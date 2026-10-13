using System.Globalization;
using System.Text.RegularExpressions;

namespace Gcam.Studio.Core.Services;

/// <summary>Shared input rules for the live setters and the Options draft.</summary>
public static partial class StudioOptionValidation
{
    public const string AmbientDosePatternText = @"^\s*(\d+(\.\d*)?|\.\d+)\s*$";
    public const string AmbientDoseMessage = "The ambient dose rate must be a number ≥ 0 in µSv/h (digits and one decimal point, e.g. 0.1).";
    public const string SeedMessage = "The seed must be blank (new each acquisition) or a nonnegative integer.";
    public const string GapMessage = "The reflector gap must be finite, nonnegative and less than pixel pitch.";
    [GeneratedRegex(AmbientDosePatternText, RegexOptions.CultureInvariant)]
    private static partial Regex AmbientPattern();
    public static string? SeedError(string text) => string.IsNullOrWhiteSpace(text)
        || int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int seed) && seed >= 0 ? null : SeedMessage;
    public static bool Nonnegative(double value) => double.IsFinite(value) && value >= 0;
    public static bool TryNonnegative(string text, out double value) => double.TryParse(text, out value) && Nonnegative(value);
    public static string? GapError(string text, double pitchMm) => TryNonnegative(text, out double value) && value / 1000 < pitchMm
        ? null : GapMessage;
    public static bool TryAmbientDose(string text, out double value)
    {
        value = double.NaN;
        return AmbientPattern().IsMatch(text) && double.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite
            | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }
}
