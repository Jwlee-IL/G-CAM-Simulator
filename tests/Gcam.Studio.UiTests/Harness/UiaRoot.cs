using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace Gcam.Studio.UiTests.Harness;

/// <summary>Diagnostics of the owned window's UI Automation root, for the focus preparation and the failure tree dump.</summary>
/// <remarks>The managed client exposes a root's FrameworkId but not its provider description (UIA property 30107), so the
/// description comes from the native client (<c>CUIAutomation</c>, built into Windows) for a fresh element from the same
/// window handle: what the window answers now, beside the FrameworkId of the root the harness holds. Best effort: a failure
/// is reported in the text, never thrown.</remarks>
public static class UiaRoot
{
    private const int ProviderDescriptionPropertyId = 30107;

    /// <summary>Reads the held root (managed client) and the window's current provider description (native client).</summary>
    public static FocusPreparation.RootState Describe(AutomationElement root)
    {
        string framework = root.Current.FrameworkId;
        bool ids = root.FindFirst(TreeScope.Descendants,
            new NotCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, ""))) is not null;
        return new(framework, ProviderDescription(new IntPtr(root.Current.NativeWindowHandle)), ids);
    }

    /// <summary>The provider description UI Automation gives for a fresh root element of <paramref name="hwnd"/>.</summary>
    public static string ProviderDescription(IntPtr hwnd)
    {
        try
        {
            if (hwnd == IntPtr.Zero) return "unavailable: no window handle";
            var automation = (IUIAutomation)new CUIAutomation();
            try
            {
                var element = automation.ElementFromHandle(hwnd);
                try { return element.GetCurrentPropertyValue(ProviderDescriptionPropertyId) as string ?? "unavailable: no description"; }
                finally { Marshal.ReleaseComObject(element); }
            }
            finally { Marshal.ReleaseComObject(automation); }
        }
        catch (Exception e) when (e is COMException or InvalidCastException or InvalidOperationException or ArgumentException)
        {
            return "unavailable: " + e.GetType().Name;
        }
    }

    // Minimal vtable declarations (UIAutomationClient.h): only the called slots have real signatures, and every slot before
    // them is declared in order so the called ones keep their positions.
    [ComImport, Guid("ff48dba4-60ef-4201-aa87-54103eef594e")]
    private class CUIAutomation { }

    [ComImport, Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomation
    {
        void CompareElements();
        void CompareRuntimeIds();
        void GetRootElement();
        [return: MarshalAs(UnmanagedType.Interface)] IUIAutomationElement ElementFromHandle(IntPtr hwnd);
    }

    [ComImport, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationElement
    {
        void SetFocus();
        void GetRuntimeId();
        void FindFirst();
        void FindAll();
        void FindFirstBuildCache();
        void FindAllBuildCache();
        void BuildUpdatedCache();
        [return: MarshalAs(UnmanagedType.Struct)] object GetCurrentPropertyValue(int propertyId);
    }
}
