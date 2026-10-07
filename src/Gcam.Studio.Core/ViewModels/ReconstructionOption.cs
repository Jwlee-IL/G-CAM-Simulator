using Gcam.Configuration;

namespace Gcam.Studio.Core.ViewModels;

/// <summary>One entry of the Imaging reconstruction selector.</summary>
public sealed record ReconstructionOption(DecoderMethod Method, string Label)
{
    public override string ToString() => Label;
}
