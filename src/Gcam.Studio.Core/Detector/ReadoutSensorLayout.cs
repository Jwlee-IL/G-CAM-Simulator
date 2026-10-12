namespace Gcam.Studio.Core.Detector;

/// <summary>Normalised sensor and row-chain coordinates inside the four-output frame.</summary>
public static class ReadoutSensorLayout
{
    public static double CellSize(int sensors) => 1.0 / (sensors + 3);
    public static double Centre(int index, int sensors) => (index + 2) * CellSize(sensors);
}
