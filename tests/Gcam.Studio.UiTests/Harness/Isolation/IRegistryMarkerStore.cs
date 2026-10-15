namespace Gcam.Studio.UiTests.Harness.Isolation;

/// <summary>Only the desktop probe uses the native store; headless tests inject a fake.</summary>
public interface IRegistryMarkerStore
{
    string NativeUserHive { get; }
    bool TryCreateNew(string relativeKey, string valueName, string valueData);
}
