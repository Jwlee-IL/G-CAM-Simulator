using Gcam.EvidenceProbe;

if (args.Length != 7 || args[0] != "studio-readout-v4")
    throw new ArgumentException("studio-readout-v4 seed samples recipe geometry phase family-hash");
StudioReadoutRecipeV4.Run(int.Parse(args[1]), args[2], args[3], args[4], args[5], args[6]);
