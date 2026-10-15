namespace Gcam.Studio.Core.Reporting;

/// <summary>A detached copy of the displayed Imaging measurement row.</summary>
public sealed record ReportMeasurement(string Name, string Pane, string Kind, string Value, string Detail);
