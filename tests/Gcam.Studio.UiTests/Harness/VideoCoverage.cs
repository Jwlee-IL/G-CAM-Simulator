using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Explicit recorder coverage and two deliberate exclusions. New unwrapped desktop methods fail the inventory test.</summary>
public static class VideoCoverage
{
    public static readonly IReadOnlyDictionary<Type, string> Exclusions = new Dictionary<Type, string>
    {
        [typeof(ObservedRunTests)] = "Observation boundary: launch-idle-exit without automation or capture",
        [typeof(PlotViewTests)] = "Visible redraw timing benchmark: recorder would change measured cost"
    };
    public static IEnumerable<MethodInfo> DesktopMethods => typeof(VideoCoverage).Assembly.GetTypes().SelectMany(t => t.GetMethods())
        .Where(m => m.GetCustomAttribute<DesktopFactAttribute>() is not null || m.GetCustomAttribute<DesktopTheoryAttribute>() is not null);
    public static string CurrentId(string fallback, params string[] arguments)
    {
        var method = new StackTrace().GetFrames().Select(f => f.GetMethod()).OfType<MethodInfo>().FirstOrDefault(m =>
            m.GetCustomAttribute<DesktopFactAttribute>() is not null || m.GetCustomAttribute<DesktopTheoryAttribute>() is not null)
            // An optimised tail call can drop the test's frame; the scenario name then still names one method.
            ?? Single(DesktopMethods.Where(m => m.Name == fallback || m.GetCustomAttribute<ManualChapterAttribute>()?.Scenario == fallback));
        return VideoEntry.CaseId(method?.DeclaringType?.Name ?? "Scenario", method?.Name ?? fallback, arguments);
    }
    private static MethodInfo? Single(IEnumerable<MethodInfo> methods) => methods.Take(2).ToArray() is [var one] ? one : null;
    public static bool Wrapped(MethodInfo method)
    {
        var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(c => unchecked((ushort)c.Value));
        byte[] bytes = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        for (int i = 0; i < bytes.Length;)
        {
            ushort value = bytes[i++]; if (value == 0xfe) value = (ushort)(0xfe00 | bytes[i++]);
            var op = codes[value];
            if (op.OperandType == OperandType.InlineMethod)
            {
                var called = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, i));
                if (called?.DeclaringType == typeof(Scenario) || called?.DeclaringType == typeof(ManualScenario)) return true;
            }
            i += op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, i),
                _ => 4
            };
        }
        return false;
    }
}
