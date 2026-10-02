static class Lik
{
    /// <summary>Poisson log-likelihood (dropping log n!) with intensity A ≥ 0 and flat background B ≥ 0 per pixel profiled.</summary>
    public static double Profile(double[] n, double[] m, bool background, out double A, out double B)
    {
        int P = n.Length; double Sn = 0, Sm = 0;
        for (int i = 0; i < P; i++) { Sn += n[i]; Sm += m[i]; }
        A = Sn / Sm; B = 0;
        double gB0 = -P; bool bad = false;
        for (int i = 0; i < P; i++) if (n[i] > 0) { if (m[i] <= 0) { bad = true; break; } gB0 += n[i] / (A * m[i]); }
        if (!background || (!bad && gB0 <= 0)) { if (bad) return double.NegativeInfinity; return Val(n, m, A, 0); }
        A = 0.9 * Sn / Sm; B = 0.1 * Sn / P;
        double cur = Val(n, m, A, B);
        for (int it = 0; it < 60; it++)
        {
            double gA = -Sm, gBv = -P, hAA = 0, hAB = 0, hBB = 0;
            for (int i = 0; i < P; i++)
            {
                if (n[i] <= 0) continue;
                double l = A * m[i] + B, r = n[i] / l, r2 = r / l;
                gA += r * m[i]; gBv += r; hAA -= r2 * m[i] * m[i]; hAB -= r2 * m[i]; hBB -= r2;
            }
            double det = hAA * hBB - hAB * hAB;
            double dA = -(hBB * gA - hAB * gBv) / det, dB = -(-hAB * gA + hAA * gBv) / det;
            double t = 1, nv = double.NegativeInfinity, nA = A, nB = B;
            for (int ls = 0; ls < 40; ls++)
            {
                nA = Math.Max(0, A + t * dA); nB = Math.Max(0, B + t * dB);
                nv = Val(n, m, nA, nB);
                if (nv >= cur) break;
                t /= 2;
            }
            if (!(nv >= cur)) break;
            bool done = nv - cur < 1e-10;
            A = nA; B = nB; cur = nv;
            if (done) break;
        }
        return cur;
    }

    static double Val(double[] n, double[] m, double A, double B)
    {
        double s = 0;
        for (int i = 0; i < n.Length; i++)
        {
            double l = A * m[i] + B;
            if (n[i] > 0) { if (l <= 0) return double.NegativeInfinity; s += n[i] * Math.Log(l); }
            s -= l;
        }
        return s;
    }
}
