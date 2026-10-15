using Gcam.Studio.Core.Reporting;

namespace Gcam.Studio.Tests;

public sealed class ReportTextWrappingTests
{
    [Fact]
    public void Wrap_UsesWordBoundariesAndKeepsParagraphs_ForBodyAndCellWidths()
    {
        Assert.Equal(new[] { "alpha beta", "gamma", "", "next line" },
            ReportTextWrapping.Wrap("alpha beta gamma\n\nnext line", 10, s => s.Length));
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, ReportTextWrapping.Wrap("alpha beta gamma", 6, s => s.Length));
        Assert.Equal(new[] { "alpha beta", "gamma" }, ReportTextWrapping.Wrap("alpha beta gamma", 11, s => s.Length));
    }

    [Fact]
    public void Wrap_SplitsOnlyTheOversizedWord_WithoutLosingCharacters()
    {
        Assert.Equal(new[] { "alpha", "abcdefghij", "klm beta" },
            ReportTextWrapping.Wrap("alpha abcdefghijklm beta", 10, s => s.Length));
        var lines = ReportTextWrapping.Wrap("  abcdefghijklm", 6, s => s.Length);
        Assert.Equal("abcdefghijklm", string.Concat(lines).Trim());
        Assert.All(lines, line => Assert.InRange(line.Length, 1, 6));
    }
}
