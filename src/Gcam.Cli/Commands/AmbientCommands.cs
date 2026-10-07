using System.Text.Json;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Simulation;

namespace Gcam.Cli;

/// <summary>Explicit physical live-time route, separate from weighted photon-budget studies.</summary>
internal static class AmbientCommands
{
    internal static int RunUncollided(string[] args)
    {
        if (args.Length is < 2 or > 3)
        { Console.Error.WriteLine("Usage: montecarlo ambient-uncollided <kernel-request.json> [output.json]"); return 1; }
        using var input = JsonDocument.Parse(File.ReadAllText(args[1]));
        var request = input.RootElement;
        double yield = request.GetProperty("PhotonYieldPerDecay").GetDouble();
        double density = request.GetProperty("SoilDensityGPerCm3").GetDouble();
        double soilMu = request.GetProperty("SoilMuPerCm").GetDouble();
        double airMu = request.GetProperty("AirMuPerCm").GetDouble();
        double height = request.GetProperty("HeightCm").GetDouble();
        int histories = request.GetProperty("Histories").GetInt32();
        int bins = request.GetProperty("ZenithBins").GetInt32();
        int seed = request.GetProperty("Seed").GetInt32();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var mc = HalfSpaceUncollided.Measure(yield, density, soilMu, airMu, height, histories, bins, new Gcam.Core.DefaultRandom(seed));
        double analytic = HalfSpaceUncollided.FluencePerBqKg(yield, density, soilMu, airMu, height);
        var angular = Enumerable.Range(0, bins).Select(i => new
        {
            LowCosine = (double)i / bins, HighCosine = (double)(i + 1) / bins,
            Accepted = mc.ZenithCounts[i],
            McFluence = mc.FluenceNormalizationPerBqKg * mc.ZenithCounts[i] / histories,
            AnalyticFluence = HalfSpaceUncollided.FluencePerBqKg(yield, density, soilMu, airMu, height, (double)i / bins, (double)(i + 1) / bins)
        }).ToArray();
        double totalProbability = mc.FluenceNormalizationPerBqKg == 0 ? 0 : analytic / mc.FluenceNormalizationPerBqKg;
        double acceptanceSigma = mc.FluenceNormalizationPerBqKg * Math.Sqrt(totalProbability * (1 - totalProbability) / histories);
        bool passes = Math.Abs(mc.FluencePerBqKg - analytic) <= 6 * acceptanceSigma
            && angular.All(b =>
            {
                double p = mc.FluenceNormalizationPerBqKg == 0 ? 0 : b.AnalyticFluence / mc.FluenceNormalizationPerBqKg;
                return Math.Abs(b.McFluence - b.AnalyticFluence) <= 6 * mc.FluenceNormalizationPerBqKg * Math.Sqrt(p * (1 - p) / histories);
            });
        string json = JsonSerializer.Serialize(new
        {
            SchemaVersion = 1, Kind = "uncollided-kernel-check-NOT-VALIDATED", IsValidatedSpectrum = false,
            Note = "Not a terrestrial spectrum, not a collided transport calculation, and not UNSCEAR validation.",
            Request = request, Mc = mc, AnalyticFluence = analytic,
            Ratio = analytic > 0 ? (double?)(mc.FluencePerBqKg / analytic) : null,
            RatioStandardError = analytic > 0 ? (double?)(mc.StandardErrorPerBqKg / analytic) : null,
            Angular = angular, AcceptanceSigmaMultiplier = 6, PassesUncollidedKernelCheck = passes,
            ComputeSeconds = watch.Elapsed.TotalSeconds
        }, new JsonSerializerOptions { WriteIndented = true });
        if (args.Length == 3) File.WriteAllText(args[2], json);
        Console.WriteLine(json);
        return passes ? 0 : 2;
    }

    /// <summary>AB-4 collided soil/air generator: every chain × outer seed, the versioned NOT-VALIDATED spectrum file and
    /// the validation record ((a) uncollided vs the analytic kernel under a six-SE rule; (b) air kerma per Bq/kg against
    /// UNSCEAR with its MC standard error over the outer seeds — reported, never judged pass/fail here, per AB-4d).</summary>
    internal static int RunTerrestrial(string[] args)
    {
        if (args.Length != 3) { Console.Error.WriteLine("Usage: montecarlo ambient-terrestrial <generator-request.json> <output-folder>"); return 1; }
        using var input = JsonDocument.Parse(File.ReadAllText(args[1]));
        var request = input.RootElement;
        var catalog = TerrestrialCatalog.Load(request.GetProperty("CatalogPath").GetString()!, request.GetProperty("CatalogSha256").GetString());
        var materials = SoilAirMaterials.Load(request.GetProperty("MaterialsPath").GetString()!, request.GetProperty("MaterialsSha256").GetString());
        var o = request.GetProperty("Options");
        var options = new SoilAirTransportOptions
        {
            HeightCm = o.GetProperty("HeightCm").GetDouble(), SlabHalfWidthCm = o.GetProperty("SlabHalfWidthCm").GetDouble(),
            DepthSamplingFactor = o.GetProperty("DepthSamplingFactor").GetDouble(), TransportCutoffKeV = o.GetProperty("TransportCutoffKeV").GetDouble(),
            ZenithBins = o.GetProperty("ZenithBins").GetInt32(), FluorescenceEnergyPoints = o.GetProperty("FluorescenceEnergyPoints").GetInt32(),
            ContinuumEdgesKeV = o.GetProperty("ContinuumEdgesKeV").EnumerateArray().Select(e => e.GetDouble()).ToArray()
        };
        long histories = request.GetProperty("HistoriesPerSeed").GetInt64();
        int[] seeds = request.GetProperty("Seeds").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        var unscear = request.GetProperty("UnscearNGyPerHourPerBqKg");
        Directory.CreateDirectory(args[2]);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var transports = catalog.Chains.Select(c => new SoilAirTransport(c, materials, options)).ToArray();
        var jobs = (from c in Enumerable.Range(0, catalog.Chains.Length) from s in seeds select (Chain: c, Seed: s)).ToArray();
        var tallies = new SoilAirTally[jobs.Length];
        // Each (chain, seed) is one independent single-stream run, so results do not depend on scheduling.
        Parallel.For(0, jobs.Length, j => tallies[j] = transports[jobs[j].Chain].Run(histories,
            DefaultRandom.FromKey(DefaultRandom.Key(jobs[j].Seed, 30001u + (uint)jobs[j].Chain))));
        double seconds = watch.Elapsed.TotalSeconds;
        double k = SoilAirTransport.KermaToNGyPerHour;
        var pooled = new Dictionary<string, SoilAirTally>();
        var chainResults = new List<object>();
        var validationPasses = new List<bool>();
        for (int c = 0; c < catalog.Chains.Length; c++)
        {
            var chain = catalog.Chains[c];
            var runs = Enumerable.Range(0, jobs.Length).Where(j => jobs[j].Chain == c).Select(j => tallies[j]).ToArray();
            var pool = Pool(runs);
            pooled[chain.Name] = pool;
            double[] perSeed = runs.Select(r => r.KermaTotal / r.Histories * k).ToArray();
            double mean = perSeed.Average(), se = Math.Sqrt(perSeed.Sum(v => (v - mean) * (v - mean)) / (perSeed.Length - 1) / perSeed.Length);
            double reference = unscear.GetProperty(chain.Name).GetDouble();
            double Share(Func<SoilAirTally, double> part) => runs.Average(r => part(r) / r.KermaTotal);
            double PooledMean(double sum) => sum / pool.Histories * k;
            double PooledSe(double sum, double sumSq)
            {
                double m = sum / pool.Histories;
                return Math.Sqrt(Math.Max(0, sumSq / pool.Histories - m * m) / (pool.Histories - 1)) * k;
            }
            // Analytic references for the uncollided part (slab-averaged, like the estimator).
            double analyticKerma = 0, analyticKermaWithCoherent = 0;
            foreach (var line in chain.Lines)
            {
                double muEn = line.EnergyKeV * materials.AirMuEnCm2PerG(line.EnergyKeV) * k;
                analyticKerma += muEn * SoilAirTransport.SlabAveragedUncollided(line, materials, options, 0, 1);
                analyticKermaWithCoherent += muEn * SoilAirTransport.SlabAveragedUncollided(line, materials, options, 0, 1, includeCoherent: true);
            }
            var check = UncollidedCheckCore(chain, materials, options, pool);
            validationPasses.Add(check.Passes);
            chainResults.Add(new
            {
                chain.Name, Lines = chain.Lines.Length, Seeds = seeds.Length, HistoriesPerSeed = histories,
                KermaNGyPerHourPerBqKgPerSeed = perSeed, KermaNGyPerHourPerBqKg = mean, KermaStandardErrorOverSeeds = se,
                UnscearNGyPerHourPerBqKg = reference, RatioToUnscear = mean / reference, RatioStandardError = se / reference,
                KermaShares = new
                {
                    Uncollided = Share(r => r.KermaUncollided), AnnihilationUncollided = Share(r => r.KermaAnnihilation),
                    Scattered = Share(r => r.KermaScattered), Above1332KeV = Share(r => r.KermaAbove1332KeV),
                    Below10KeV = Share(r => r.KermaBelow10KeV)
                },
                PooledKerma = new
                {
                    Total = PooledMean(pool.KermaTotal), TotalPerHistorySe = PooledSe(pool.KermaTotal, pool.KermaSumSq),
                    Uncollided = PooledMean(pool.KermaUncollided), Annihilation = PooledMean(pool.KermaAnnihilation),
                    Scattered = PooledMean(pool.KermaScattered), Above1332KeV = PooledMean(pool.KermaAbove1332KeV),
                    Below10KeV = PooledMean(pool.KermaBelow10KeV)
                },
                UncollidedKermaAnalytic = analyticKerma,
                HybridKermaAnalyticUncollidedPlusMcRest = analyticKerma + PooledMean(pool.KermaAnnihilation + pool.KermaScattered),
                CoherentTreatment = new
                {
                    UncollidedKermaWithoutCoherent = analyticKerma, UncollidedKermaWithCoherentInAttenuation = analyticKermaWithCoherent,
                    Note = "The transport treats coherent scattering as no interaction. The analytic uncollided kerma with coherent in the attenuation is the opposite extreme (every coherent event removes the photon); the truth lies between."
                },
                FluorescenceKermaBound = new
                {
                    Mean = PooledMean(pool.FluorescenceKermaBound), PerHistorySe = PooledSe(pool.FluorescenceKermaBound, pool.FluorescenceKermaBoundSumSq),
                    ShareOfKerma = pool.FluorescenceKermaBound / pool.KermaTotal,
                    ShareFromSoilAbsorptions = pool.FluorescenceKermaBoundFromSoil / pool.FluorescenceKermaBound
                },
                FluencePerBqKg = new
                {
                    Uncollided = pool.FluenceUncollided / pool.Histories, Annihilation = pool.FluenceAnnihilation / pool.Histories,
                    Scattered = pool.FluenceScattered / pool.Histories, ScatteredBelowFirstEdge = pool.ScatteredFluenceBelowFirstEdge / pool.Histories,
                    ScatteredAboveLastEdge = pool.ScatteredFluenceAboveLastEdge / pool.Histories
                },
                Counters = new { pool.Interactions, pool.PairEvents, pool.CutoffTerminations },
                UncollidedValidation = new { check.Passes, check.Detail }
            });
        }
        string id = request.GetProperty("SpectrumId").GetString()!;
        var spectrum = TerrestrialSpectrumBuilder.Build(catalog, materials, options, pooled, id, request.GetProperty("SpectrumReference").GetString()!);
        string spectrumPath = Path.Combine(args[2], id + ".json");
        File.WriteAllText(spectrumPath, TerrestrialSpectrumBuilder.Serialize(spectrum));
        string fileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(spectrumPath))).ToLowerInvariant();
        File.WriteAllText(spectrumPath + ".sha256", fileHash + "\n");
        string json = JsonSerializer.Serialize(new
        {
            SchemaVersion = 1, Kind = "ambient-terrestrial-generator", IsValidatedSpectrum = false,
            Note = "AB-4d: the UNSCEAR ratios are reported for the author's decision; no acceptance tolerance is applied here.",
            Request = request, CatalogSha256 = request.GetProperty("CatalogSha256").GetString(), MaterialsId = materials.Id,
            MaterialsSha256 = materials.FileSha256, Chains = chainResults,
            Spectrum = new { File = id + ".json", FileSha256 = fileHash, spectrum.ContentHash, Lines = spectrum.Lines.Length,
                ContinuumBins = spectrum.Continuum.Length, TableRows = spectrum.EnergyZenith!.Length, NotIncluded = spectrum.NotIncluded!.Length },
            ComputeSeconds = seconds, Threads = Environment.ProcessorCount
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(args[2], "generator-results.json"), json);
        Console.WriteLine(json.Length > 4000 ? json[..4000] + " …" : json);
        return validationPasses.All(v => v) ? 0 : 2;
    }

    private static SoilAirTally Pool(SoilAirTally[] runs)
    {
        static double[] Add(IEnumerable<double[]> arrays) => arrays.Aggregate((a, b) => a.Zip(b, (x, y) => x + y).ToArray());
        return new SoilAirTally
        {
            Chain = runs[0].Chain, Histories = runs.Sum(r => r.Histories), ZenithBins = runs[0].ZenithBins, LineEnergiesKeV = runs[0].LineEnergiesKeV,
            UncollidedSum = Add(runs.Select(r => r.UncollidedSum)), UncollidedSumSq = Add(runs.Select(r => r.UncollidedSumSq)),
            UncollidedCount = runs.Select(r => r.UncollidedCount).Aggregate((a, b) => a.Zip(b, (x, y) => x + y).ToArray()),
            AnnihilationSum = Add(runs.Select(r => r.AnnihilationSum)), ContinuumSum = Add(runs.Select(r => r.ContinuumSum)),
            ScatteredFluenceBelowFirstEdge = runs.Sum(r => r.ScatteredFluenceBelowFirstEdge), ScatteredFluenceAboveLastEdge = runs.Sum(r => r.ScatteredFluenceAboveLastEdge),
            FluenceUncollided = runs.Sum(r => r.FluenceUncollided), FluenceAnnihilation = runs.Sum(r => r.FluenceAnnihilation),
            FluenceScattered = runs.Sum(r => r.FluenceScattered), KermaUncollided = runs.Sum(r => r.KermaUncollided),
            KermaAnnihilation = runs.Sum(r => r.KermaAnnihilation), KermaScattered = runs.Sum(r => r.KermaScattered),
            KermaSumSq = runs.Sum(r => r.KermaSumSq), KermaAbove1332KeV = runs.Sum(r => r.KermaAbove1332KeV),
            KermaBelow10KeV = runs.Sum(r => r.KermaBelow10KeV), KermaBelowFirstEdge = runs.Sum(r => r.KermaBelowFirstEdge),
            FluorescenceKermaBound = runs.Sum(r => r.FluorescenceKermaBound), FluorescenceKermaBoundSumSq = runs.Sum(r => r.FluorescenceKermaBoundSumSq),
            FluorescenceKermaBoundFromSoil = runs.Sum(r => r.FluorescenceKermaBoundFromSoil),
            Interactions = runs.Sum(r => r.Interactions), PairEvents = runs.Sum(r => r.PairEvents), CutoffTerminations = runs.Sum(r => r.CutoffTerminations)
        };
    }

    /// <summary>Validation (a), rule declared before running: |MC − analytic| ≤ 6 standard errors for the chain's total
    /// uncollided fluence, for each upward zenith bin of the total, and for every bin of the five lines with the largest
    /// uncollided kerma. SE from the pooled per-history second moments (each history scores at most one uncollided
    /// crossing, so the per-cell squares are per-history moments). The normal approximation behind a k·SE rule needs
    /// counts: a cell with fewer than 30 scored crossings is reported as "not tested", never as passed (it carries a
    /// negligible part of the kerma — e.g. the K-40 Ar X-rays and its 2e-5 annihilation line). Downward bins must be
    /// exactly zero.</summary>
    private static (bool Passes, object Detail) UncollidedCheckCore(TerrestrialChain chain, SoilAirMaterials materials,
        SoilAirTransportOptions options, SoilAirTally pool)
    {
        int zb = options.ZenithBins, half = zb / 2;
        long n = pool.Histories;
        const double sigmas = 6;
        var cells = new List<object>();
        bool passes = true;
        const long minimumCrossings = 30;
        int untested = 0;
        (double Mc, double Se, long Count) Cell(IEnumerable<int> lines, IEnumerable<int> bins)
        {
            double sum = 0, sq = 0;
            long count = 0;
            foreach (int l in lines) foreach (int z in bins)
            { sum += pool.UncollidedSum[l * zb + z]; sq += pool.UncollidedSumSq[l * zb + z]; count += pool.UncollidedCount[l * zb + z]; }
            double m = sum / n;
            return (m, Math.Sqrt(Math.Max(0, sq / n - m * m) / (n - 1)), count);
        }
        void Compare(string label, int[] lines, int[] bins)
        {
            var (mc, se, count) = Cell(lines, bins);
            double analytic = lines.Sum(l => bins.Sum(z => SoilAirTransport.SlabAveragedUncollided(chain.Lines[l], materials, options,
                TerrestrialSpectrumBuilder.Cosine(z, zb), TerrestrialSpectrumBuilder.Cosine(z + 1, zb))));
            bool tested = count >= minimumCrossings;
            bool ok = tested && Math.Abs(mc - analytic) <= sigmas * se;
            if (tested) passes &= ok; else untested++;
            cells.Add(new { Label = label, Crossings = count, McFluence = mc, StandardError = se, AnalyticSlabAveraged = analytic,
                Ratio = analytic > 0 ? mc / analytic : double.NaN, Z = se > 0 ? (mc - analytic) / se : 0,
                Result = tested ? (ok ? "pass" : "FAIL") : "not tested (< 30 crossings)" });
        }
        var all = Enumerable.Range(0, chain.Lines.Length).ToArray();
        var up = Enumerable.Range(half, half).ToArray();
        Compare("all lines, all upward bins", all, up);
        foreach (int z in up) Compare($"all lines, bin [{TerrestrialSpectrumBuilder.Cosine(z, zb):0.0},{TerrestrialSpectrumBuilder.Cosine(z + 1, zb):0.0}]", all, [z]);
        var principal = all.OrderByDescending(l => chain.Lines[l].EnergyKeV * materials.AirMuEnCm2PerG(chain.Lines[l].EnergyKeV)
            * SoilAirTransport.SlabAveragedUncollided(chain.Lines[l], materials, options, 0, 1)).Take(5).ToArray();
        foreach (int l in principal)
        {
            string name = $"{chain.Lines[l].Nuclide} {chain.Lines[l].EnergyKeV} keV";
            Compare(name + ", all upward bins", [l], up);
            foreach (int z in up) Compare($"{name}, bin [{TerrestrialSpectrumBuilder.Cosine(z, zb):0.0},{TerrestrialSpectrumBuilder.Cosine(z + 1, zb):0.0}]", [l], [z]);
        }
        double downward = Enumerable.Range(0, half).Sum(z => all.Sum(l => pool.UncollidedSum[l * zb + z]));
        passes &= downward == 0;
        return (passes, new { Rule = "|MC - analytic| <= 6 SE (pooled per-history SE) for cells with >= 30 crossings; fewer = not tested; downward uncollided bins exactly zero",
            Histories = n, DownwardUncollidedSum = downward, UntestedCells = untested, Cells = cells });
    }

    /// <summary>One outer seed of the AB-7 gate study / AB-9 count-gate re-measurement (<see cref="AmbientGateStudy"/>).
    /// The request is the seed driver's config: it replaces Seed and RepoRoot in a clone of the committed request.</summary>
    internal static int RunGate(string[] args)
    {
        if (args.Length is < 2 or > 3) { Console.Error.WriteLine("Usage: montecarlo ambient-gate <gate-request.json> [output.json]"); return 1; }
        var request = JsonSerializer.Deserialize<AmbientGateRequest>(File.ReadAllText(args[1]),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })
            ?? throw new InvalidDataException("Empty gate request.");
        var result = AmbientGateStudy.Run(request);
        string output = args.Length == 3 ? args[2] : "ambient-gate.json";
        File.WriteAllText(output, JsonSerializer.Serialize(result));
        Console.WriteLine($"ambient-gate {request.Phase} seed {request.Seed}: {result["ComputeSeconds"]:0.0} s -> {Path.GetFileName(output)}");
        return 0;
    }

    /// <summary>AB-13 evidence families under the absolute ambient field (TODO-30 turn 8), one outer seed per run: EV-12
    /// mask / antimask, EV-02 field of view, EV-15 separation, EV-01 sweep with the AB-12 bias baseline; TODO-35 Cs-137 under
    /// Co-60 (<c>csco</c>); TODO-34 angular resolution (<c>angres</c>).</summary>
    internal static int RunEvidence(string[] args)
    {
        if (args.Length is < 2 or > 3) { Console.Error.WriteLine("Usage: montecarlo ambient-evidence <evidence-request.json> [output.json]"); return 1; }
        var request = JsonSerializer.Deserialize<AmbientEvidenceRequest>(File.ReadAllText(args[1]),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })
            ?? throw new InvalidDataException("Empty evidence request.");
        var result = request.Family switch
        {
            "antimask" => AmbientAntimaskStudy.Run(request),
            "fov" => AmbientFieldOfViewStudy.Run(request),
            "separation" => AmbientSeparationStudy.Run(request),
            "sweep" => AmbientSweepStudy.Run(request),
            "csco" => CsUnderCoStudy.Run(request),
            "angres" => AngularResolutionStudy.Run(request),
            _ => throw new ArgumentException($"Unknown evidence family '{request.Family}'.")
        };
        string output = args.Length == 3 ? args[2] : "ambient-evidence.json";
        File.WriteAllText(output, JsonSerializer.Serialize(result));
        Console.WriteLine($"ambient-evidence {request.Family} seed {request.Seed}: {result["ComputeSeconds"]:0.0} s -> {Path.GetFileName(output)}");
        return 0;
    }

    internal static int WritePlaceholder(string[] args)
    {
        if (args.Length != 2) { Console.Error.WriteLine("Usage: montecarlo ambient-placeholder <spectrum.json>"); return 1; }
        // LF line ends on every OS: the pinned hash is the hash of the bytes git stores (*.json text eol=lf).
        string hash = IncidentSpectrumFile.WriteWithSidecar(args[1], IncidentSpectrumFile.SerializeIndented(IncidentSpectrum.Placeholder()));
        Console.WriteLine($"SHA-256 {hash}");
        Console.WriteLine("NOT VALIDATED: synthetic isotropic development fixture, not an AB-4 terrestrial spectrum.");
        return 0;
    }

    /// <summary>AB-10: re-issue an accepted spectrum as a validated version. Weights, table and NotIncluded are copied
    /// unchanged; only the identifier, the validation flag, the status sentence of the reference and the acceptance record
    /// change. The ratios are read from the generator's results file (hash-pinned), never typed in. The not-validated file
    /// stays as it is.</summary>
    internal static int IssueValidated(string[] args)
    {
        if (args.Length != 3) { Console.Error.WriteLine("Usage: montecarlo ambient-issue <issue-request.json> <output-folder>"); return 1; }
        string requestDir = Path.GetDirectoryName(Path.GetFullPath(args[1]))!;
        using var input = JsonDocument.Parse(File.ReadAllText(args[1]));
        var r = input.RootElement;
        string Rel(string name) => Path.Combine(requestDir, r.GetProperty(name).GetString()!);
        var source = IncidentSpectrumFile.Load(Rel("Source"), r.GetProperty("SourceSha256").GetString()!);
        string sourceFileHash = r.GetProperty("SourceSha256").GetString()!;
        if (source.IsValidated) { Console.Error.WriteLine("The source spectrum is already validated."); return 1; }
        byte[] resultsBytes = File.ReadAllBytes(Rel("RatiosFrom"));
        if (IncidentSpectrumFile.Sha256Hex(resultsBytes) != r.GetProperty("RatiosFromSha256").GetString())
        { Console.Error.WriteLine("The ratios file does not match its pinned SHA-256."); return 1; }
        using var results = JsonDocument.Parse(resultsBytes);
        string unscear = r.GetProperty("ReferenceName").GetString()!;
        var ratios = results.RootElement.GetProperty("Chains").EnumerateArray().Select(c => new IncidentValidationRatio
        {
            Chain = c.GetProperty("Chain").GetString()!, Quantity = "air kerma at 1 m per Bq/kg, nGy/h",
            ModelValue = c.GetProperty("KermaNGyPerHourPerBqKg").GetDouble(),
            ModelStandardError = c.GetProperty("StandardErrorOverSeeds").GetDouble(),
            ReferenceValue = c.GetProperty("Unscear").GetDouble(), Reference = unscear,
            Ratio = c.GetProperty("Ratio").GetDouble(), RatioStandardError = c.GetProperty("RatioStandardError").GetDouble()
        }).ToArray();
        var v = r.GetProperty("Validation");
        var validation = new IncidentValidation
        {
            Decision = v.GetProperty("Decision").GetString()!, Date = v.GetProperty("Date").GetString()!,
            Kind = v.GetProperty("Kind").GetString()!, AgreementBandFraction = v.GetProperty("AgreementBandFraction").GetDouble(),
            Note = v.GetProperty("Note").GetString()!, Ratios = ratios,
            SupersedesId = source.Id, SupersedesContentHash = source.ContentHash, SupersedesFileSha256 = sourceFileHash
        };
        var outside = ratios.Where(x => Math.Abs(x.Ratio - 1) > validation.AgreementBandFraction).ToArray();
        if (outside.Length > 0)
        { Console.Error.WriteLine($"Ratios outside the accepted band: {string.Join(", ", outside.Select(x => x.Chain))} — not issued."); return 2; }
        string oldStatus = r.GetProperty("ReferenceStatusOld").GetString()!, newStatus = r.GetProperty("ReferenceStatusNew").GetString()!;
        int at = source.Reference.IndexOf(oldStatus, StringComparison.Ordinal);
        if (at < 0 || source.Reference.IndexOf(oldStatus, at + 1, StringComparison.Ordinal) >= 0)
        { Console.Error.WriteLine("The status sentence must occur exactly once in the reference."); return 1; }
        var issued = new IncidentSpectrum
        {
            Id = r.GetProperty("Id").GetString()!, Version = source.Version,
            Reference = source.Reference.Replace(oldStatus, newStatus, StringComparison.Ordinal),
            AngularModel = source.AngularModel, IsValidated = true,
            Lines = source.Lines, Continuum = source.Continuum, EnergyZenith = source.EnergyZenith, NotIncluded = source.NotIncluded,
            Validation = validation
        };
        if (issued.Id.Contains("NOT-VALIDATED", StringComparison.Ordinal)) { Console.Error.WriteLine("A validated identifier cannot say NOT-VALIDATED."); return 1; }
        issued.ContentHash = issued.ComputeContentHash();
        Directory.CreateDirectory(args[2]);
        string path = Path.Combine(args[2], issued.Id + ".json");
        string fileHash = IncidentSpectrumFile.WriteWithSidecar(path, IncidentSpectrumFile.SerializeCompact(issued));
        Console.WriteLine(JsonSerializer.Serialize(new { File = issued.Id + ".json", FileSha256 = fileHash, issued.ContentHash,
            Supersedes = source.Id, validation.AgreementBandFraction, Ratios = ratios.Select(x => new { x.Chain, x.Ratio, x.RatioStandardError }) },
            new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    internal static int RunFixedTime(string[] args)
    {
        if (args.Length is < 2 or > 3)
        { Console.Error.WriteLine("Usage: montecarlo ambient-fixed <scenario.json> [output.json]"); return 1; }
        var config = ConfigLoader.Load(args[1]);
        if (config.Ambient is not { } field) { Console.Error.WriteLine("An Ambient config is required."); return 1; }
        var runner = new SimulationRunner(new DefaultSimulationFactory());
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = runner.RunFixedTime(config, config.Source.AcquisitionTimeSeconds);
        string json = JsonSerializer.Serialize(new
        {
            SchemaVersion = 1, Spectrum = field.Spectrum.Id, field.Spectrum.IsValidated,
            field.Spectrum.ContentHash, Geometry = field.Geometry.ToString(), field.DoseRateMicroSvPerHour,
            DurationS = config.Source.AcquisitionTimeSeconds, config.Seed,
            Counts = result.DetectedWeight, Flood = result.DetectorImage.Raw.ToArray(),
            PixelsX = result.DetectorImage.Width, PixelsY = result.DetectorImage.Height,
            ComputeSeconds = watch.Elapsed.TotalSeconds
        }, new JsonSerializerOptions { WriteIndented = true });
        if (args.Length == 3) File.WriteAllText(args[2], json);
        Console.WriteLine(json);
        if (!field.Spectrum.IsValidated) Console.Error.WriteLine("NOT VALIDATED: development spectrum; exclude from ambient evidence.");
        return 0;
    }
}
