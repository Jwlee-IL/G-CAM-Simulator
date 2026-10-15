namespace Gcam.Studio.Core.Reporting;

/// <summary>Measured word wrapping for report text and table cells. Only an oversized word is split.</summary>
public static class ReportTextWrapping
{
    public static IReadOnlyList<string> Wrap(string text, double width, Func<string, double> measure)
    {
        var lines = new List<string>();
        foreach (string paragraph in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            string rest = paragraph;
            do
            {
                if (measure(rest) <= width) { lines.Add(rest); break; }
                // Find the largest measured prefix; no linear scan of a potentially long JSON value.
                int low = 1, high = rest.Length;
                while (low < high)
                {
                    int middle = (low + high + 1) / 2;
                    if (measure(rest[..middle]) <= width) low = middle;
                    else high = middle - 1;
                }
                int count = low;
                if (count < rest.Length && !char.IsWhiteSpace(rest[count]))
                {
                    int boundary = count;
                    while (boundary > 0 && !char.IsWhiteSpace(rest[boundary - 1])) boundary--;
                    // Leading indentation alone is not a word boundary: split the oversized word in that case.
                    if (boundary > 0 && rest[..boundary].Any(c => !char.IsWhiteSpace(c))) count = boundary;
                }
                lines.Add(rest[..count].TrimEnd());
                rest = rest[count..].TrimStart();
            } while (rest.Length > 0);
        }
        return lines;
    }
}
