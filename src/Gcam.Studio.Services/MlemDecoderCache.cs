using Gcam.Configuration;
using Gcam.Decoding;
using Gcam.Simulation;

namespace Gcam.Studio.Services;

/// <summary>TODO-36 (MD-5, MD-7): pixel-area MLEM decoders kept across refreshes. Building the matrix costs ~110 ms at the
/// default optics against ~0.33 ms per iteration, and <see cref="ImagingProjection.Project"/> runs once per channel per
/// refresh, so a decoder is built once per (grid geometry, forward pattern, closed-cell transmission, iterations, sub-cell)
/// and its matrix is prepared under the lock. A prepared decoder only reads shared arrays, so concurrent decodes are safe.</summary>
public sealed class MlemDecoderCache
{
    // A focal-plane change makes a new geometry; a few entries cover switching back and forth between channels and planes.
    private const int Capacity = 12;
    private readonly object _gate = new();
    private readonly Dictionary<Key, MlemDecoder> _decoders = [];
    private readonly Queue<Key> _order = new();

    private readonly record struct Key(Gcam.Decoding.CodedApertureGeometry Geometry, int Rank, int MosaicX, int MosaicY, bool Invert,
        double Transmission, int Iterations, Gcam.Core.SubCellMethod SubCell, int Width, int Height);

    /// <summary>Matrices built so far (a test and diagnostics hook).</summary>
    public int Builds { get; private set; }

    public MlemDecoder For(SimulationConfig config, double lineEnergyKeV)
    {
        ArgumentNullException.ThrowIfNull(config);
        var key = new Key(DefaultSimulationFactory.ReconstructionGeometry(config), config.Mask.Rank, config.Mask.MosaicX, config.Mask.MosaicY,
            config.Mask.Invert, MlemReconstruction.ClosedCellTransmission(config, lineEnergyKeV), config.Decoder.MlemIterations,
            config.Decoder.SubCellInterpolation, config.Detector.PixelsX, config.Detector.PixelsY);
        lock (_gate)
        {
            if (_decoders.TryGetValue(key, out var cached)) return cached;
            var decoder = new DefaultSimulationFactory().CreateMlemDecoder(config, lineEnergyKeV);
            decoder.SystemMatrix(key.Width, key.Height);   // build now, not lazily inside a concurrent decode
            Builds++;
            _decoders[key] = decoder;
            _order.Enqueue(key);
            while (_order.Count > Capacity) _decoders.Remove(_order.Dequeue());
            return decoder;
        }
    }
}
