using System.Text;
using Gcam.Configuration;
using Gcam.Core;
using Gcam.Detector;
using Gcam.Simulation;

namespace Gcam.Cli;

/// <summary>ASCII rendering of flood maps, reconstructions and FCFOV ghost maps for the console.</summary>
internal static class ConsoleRender
{
    internal static void RenderGhostMap(SweepResult sweep, double threshold)
    {
        int c = sweep.N / 2;
        for (int iy = sweep.N - 1; iy >= 0; iy--)
        {
            var sb = new StringBuilder();
            for (int ix = 0; ix < sweep.N; ix++)
            {
                char ch = ix == c && iy == c ? '+' : sweep.At(ix, iy).ErrorMm < threshold ? '.' : '#';
                sb.Append(ch).Append(ch);
            }
            Console.WriteLine(sb.ToString());
        }
    }

    internal static void RenderReconstruction(DetectorImage recon, double origin, double step,
                                     double trueX, double trueY, double estX, double estY)
    {
        const string ramp = " .:-=+*#%@";
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        foreach (var v in recon.Raw) { if (v < min) min = v; if (v > max) max = v; }
        double span = max - min;
        if (span <= 0) { Console.WriteLine("(flat)"); return; }

        int Idx(double phys) => (int)Math.Round((phys - origin) / step);
        bool InGrid(int gx, int gy) => gx >= 0 && gx < recon.Width && gy >= 0 && gy < recon.Height;
        int tGx = Idx(trueX), tGy = Idx(trueY), eGx = Idx(estX), eGy = Idx(estY);

        for (int y = recon.Height - 1; y >= 0; y--)
        {
            var sb = new StringBuilder();
            for (int x = 0; x < recon.Width; x++)
            {
                if (x == eGx && y == eGy) sb.Append('o');
                else if (x == tGx && y == tGy) sb.Append('T');
                else sb.Append(ramp[(int)((recon[x, y] - min) / span * (ramp.Length - 1))]);
            }
            Console.WriteLine(sb.ToString());
        }

        if (!InGrid(tGx, tGy))
            Console.WriteLine("(T off-grid: true source is outside the FCFOV — expect a ghost)");
    }

    internal static void RenderFloodMap(DetectorImage img)
    {
        const string ramp = " .:-=+*#%@";
        double max = 0.0;
        foreach (var v in img.Raw)
            if (v > max) max = v;

        if (max <= 0.0)
        {
            Console.WriteLine("(empty — no photons reached the detector)");
            return;
        }

        for (int y = img.Height - 1; y >= 0; y--)
        {
            var sb = new StringBuilder();
            for (int x = 0; x < img.Width; x++)
            {
                int idx = (int)(img[x, y] / max * (ramp.Length - 1));
                sb.Append(ramp[idx]).Append(ramp[idx]); // doubled for aspect ratio
            }
            Console.WriteLine(sb.ToString());
        }
    }
}
