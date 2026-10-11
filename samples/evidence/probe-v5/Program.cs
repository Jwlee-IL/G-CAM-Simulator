using Gcam.EvidenceProbe;

if (args.Length != 7 || args[0] != "studio-readout-v5")
    throw new ArgumentException("studio-readout-v5 seed samples recipe geometry phase family-hash");
StudioReadoutRecipeV5.Run(int.Parse(args[1]), args[2], args[3], args[4], args[5], args[6]);
