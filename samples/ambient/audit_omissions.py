"""AB-4c source-term audit. Reads public evaluated snapshots; never creates a spectrum.

python samples/ambient/audit_omissions.py --source-dir <snapshot folder> --out <audit.json>
Exit 2 means an omission cannot be certified. Feeding is used only in bounds, never as line intensity.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path


def audit(source_dir):
    snapshot = source_dir / "214bi.csv"
    digest = hashlib.sha256(snapshot.read_bytes()).hexdigest()
    expected = "8116f807ec12b5bdd8612af4fda49e9a148520a99c9757adcea3298730036adf"
    if digest != expected:
        raise ValueError("Evaluated snapshot hash differs; re-audit the source and its normalization before using it.")
    with snapshot.open(encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream))
    known = [r for r in rows if r["p_energy"] == "0" and r["decay"] == "B-" and r["intensity"]]
    # LiveChart decay radiation intensity is absolute percent per parent decay (not per selected branch).
    # This agrees with 45.45% * 0.999790 = 45.4404555% for the ENSDF 609-keV normalization.
    known_energy = sum(float(r["energy"]) * float(r["intensity"]) / 100 for r in known)
    # In the main U-chain path, Po-218 alpha populates Pb-214, then Bi-214. Discard other contributions
    # deliberately: this subset is a conservative evaluated-energy denominator, not a full chain inventory.
    occurrence = .99980
    lower_energy = known_energy * occurrence
    threshold = 1e-4
    alpha_po = .0011 / 100 * occurrence * 837
    beta_po = .020 / 100 * 259
    # Basunia 2014: intensities per 100 alpha decays and absolute alpha branch 0.00021.
    alpha_bi = occurrence * .00021 * sum(f * e / 100 for f, e in
        [(53.9, 62.5), (5.8, 253.6), (.61, 334), (.21, 498), (.25, 582)])

    def omission(parent, branch, bound, formula, references, count=None):
        return dict(ParentNuclide=parent, DecayBranch=branch,
            EnergyUpperBoundKeVPerChainDecay=bound, ChainEvaluatedEnergyLowerBoundKeV=lower_energy,
            EnergyRatioUpperBound=bound / lower_energy, Threshold=threshold,
            PassesNominalSourceApproximation=bound / lower_energy <= threshold,
            PhotonCountUpperBoundPerChainDecay=count, CountScope="Nuclear de-excitation only; atomic relaxation multiplicity not bounded by adopted nuclear levels.",
            Formula=formula, References=references,
            Convention="Bounds conditional on evaluated nominal parameters; not a statistical confidence bound or transported-kerma bound.")

    po_alpha_ref = "https://www.nndc.bnl.gov/ensnds/214/Pb/a_decay_3.097_m.pdf"
    po_beta_ref = "https://www.nndc.bnl.gov/ensnds/218/Po/adopted.pdf"
    bi_alpha_ref = "https://www.nndc.bnl.gov/ensnds/210/Tl/a_decay_19.9_m.pdf"
    certified = [
        omission("Po-218", "alpha", alpha_po, "0.0011/100 * 0.99980 * 837 keV", [po_alpha_ref], .0011 / 100 * occurrence),
        omission("Po-218", "beta-", beta_po, "0.020/100 * 259 keV", [po_beta_ref]),
        omission("Bi-214", "alpha", alpha_bi, "0.99980 * 0.00021 * sum(relative feeding/100 * excitation energy)",
            [bi_alpha_ref, "https://www.nndc.bnl.gov/ensnds/210/Tl/adopted.pdf"],
            occurrence * .00021 * sum(f * steps / 100 for f, steps in [(53.9, 1), (5.8, 2), (.61, 3), (.21, 4), (.25, 5)]))
    ]
    # Four inferred transitions explicitly have no measured photon intensity. Quoted feeding for the
    # 2544.92-keV level is a LOWER limit, so it cannot be used as an upper-bound population.
    # Reserve the entire beta Q value for the unknown component. This is conservative, not its estimated yield.
    unresolved_beta = occurrence * .999790 * 3269
    stopped = omission("Bi-214", "unresolved beta photon component", unresolved_beta,
        "0.99980 * 0.999790 * 3269 keV; entire branch energy reserved as a conservative unknown-component bound",
        ["https://www.nndc.bnl.gov/ensnds/214/Po/beta_decay.pdf"])
    stopped["InferredUnobservedTransitionsKeV"] = [36.8, 61.0, 71.1, 104.4]
    stopped["Reason"] = "Evaluated photon intensities are absent; selected feeding values explicitly exclude unknown transitions or are lower limits. No source upper bound below 1e-4 is established. This is not a claim that the actual omitted yield exceeds the threshold."
    return dict(SchemaVersion=1, Task="TODO-30", Turn=5, Date="2026-10-03",
        Status="blocked_on_unresolved_Bi214_beta_photon_component", IsValidatedSpectrum=False,
        Source=dict(Snapshot="214bi.csv", Sha256=digest,
            Url="https://nds.iaea.org/relnsd/v1/data?fields=decay_rads&nuclides=214bi&rad_types=g",
            Evaluation="Zhu and McCutchan, Nuclear Data Sheets 175, 1 (2021); cutoff 2021-05-01",
            KnownGroundStateBetaRows=len(known), KnownEmittedEnergyKeVPerBi214Decay=known_energy,
            IntensityConvention="Absolute percent per parent decay; gamma/x-ray rows with evaluated intensities, no unspecified-intensity row assigned zero."),
        SourceOmissions=certified, CertifiedOmissionEnergySumKeV=sum(o["EnergyUpperBoundKeVPerChainDecay"] for o in certified),
        CertifiedOmissionRatioSum=sum(o["EnergyRatioUpperBound"] for o in certified),
        StoppedOmissions=[stopped], FullChainAuditComplete=False, SoilTransportHistories=0,
        UnscearComparison=None, GateAcquisitionsPerBound=0, EvidenceRunsPerBound=0,
        ApprovalRequests=[])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=Path, default=Path(__file__).parent / "source-data" / "v1")
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    result = audit(args.source_dir)
    args.out.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(result["Status"])
    return 2 if result["StoppedOmissions"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
