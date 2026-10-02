/// <summary>Quadratic response surface: least-squares full quadratic through a 3^d factorial design around a centre.
/// Smooths the small-scale roughness of the CRN Monte Carlo likelihood and adapts to the parameter scales.</summary>
static class Rsm
{
    /// <summary>Returns (centre of the fitted maximum, value of the fitted maximum, Hessian in the given coords, residual RMS, ok).</summary>
    public static (double[] x, double f, double[,] H, double rms, bool ok) Fit(Func<double[], double> f, double[] c, double[] h)
    {
        int d = c.Length, n = (int)Math.Pow(3, d), p = 1 + d + d * (d + 1) / 2;
        var X = new double[n, p]; var y = new double[n]; var pts = new double[n][];
        for (int k = 0; k < n; k++)
        {
            var u = new double[d]; int r = k; for (int j = 0; j < d; j++) { u[j] = r % 3 - 1; r /= 3; }
            var x = new double[d]; for (int j = 0; j < d; j++) x[j] = c[j] + u[j] * h[j];
            pts[k] = x; y[k] = f(x);
            int col = 0; X[k, col++] = 1; for (int j = 0; j < d; j++) X[k, col++] = u[j];
            for (int i = 0; i < d; i++) for (int j = i; j < d; j++) X[k, col++] = u[i] * u[j];
        }
        if (y.Any(v => !double.IsFinite(v))) return (c, double.NegativeInfinity, new double[d, d], double.NaN, false);
        var beta = LeastSquares(X, y, n, p);
        // gradient g and Hessian Hu in unit coords
        var g = new double[d]; var Hu = new double[d, d]; int cc = 1 + d;
        for (int j = 0; j < d; j++) g[j] = beta[1 + j];
        for (int i = 0; i < d; i++) for (int j = i; j < d; j++) { double b = beta[cc++]; if (i == j) Hu[i, i] = 2 * b; else { Hu[i, j] = b; Hu[j, i] = b; } }
        double rss = 0; for (int k = 0; k < n; k++) { double e = y[k] - Pred(beta, pts[k], c, h); rss += e * e; }
        double rms = Math.Sqrt(rss / Math.Max(1, n - p));
        // maximum: u* = −Hu⁻¹ g (requires negative definite)
        var inv = Inverse(Hu, d); bool ok = inv is not null && NegDef(Hu, d);
        var us = new double[d];
        if (ok) for (int i = 0; i < d; i++) { double s = 0; for (int j = 0; j < d; j++) s -= inv![i, j] * g[j]; us[i] = s; }
        var xs = new double[d]; for (int j = 0; j < d; j++) xs[j] = c[j] + us[j] * h[j];
        double fs = ok ? Pred(beta, xs, c, h) : y.Max();
        var H = new double[d, d]; for (int i = 0; i < d; i++) for (int j = 0; j < d; j++) H[i, j] = Hu[i, j] / (h[i] * h[j]);
        return (xs, fs, H, rms, ok);
    }

    static double Pred(double[] beta, double[] x, double[] c, double[] h)
    {
        int d = x.Length; var u = new double[d]; for (int j = 0; j < d; j++) u[j] = (x[j] - c[j]) / h[j];
        double s = beta[0]; int col = 1; for (int j = 0; j < d; j++) s += beta[col++] * u[j];
        for (int i = 0; i < d; i++) for (int j = i; j < d; j++) s += beta[col++] * u[i] * u[j];
        return s;
    }

    public static bool NegDef(double[,] A, int d)
    {   // Sylvester on −A
        for (int k = 1; k <= d; k++) { var m = new double[k, k]; for (int i = 0; i < k; i++) for (int j = 0; j < k; j++) m[i, j] = -A[i, j]; if (Det(m, k) <= 0) return false; }
        return true;
    }
    static double Det(double[,] a, int n)
    {
        var m = (double[,])a.Clone(); double det = 1;
        for (int i = 0; i < n; i++)
        {
            int piv = i; for (int r = i + 1; r < n; r++) if (Math.Abs(m[r, i]) > Math.Abs(m[piv, i])) piv = r;
            if (m[piv, i] == 0) return 0;
            if (piv != i) { for (int cidx = 0; cidx < n; cidx++) (m[i, cidx], m[piv, cidx]) = (m[piv, cidx], m[i, cidx]); det = -det; }
            det *= m[i, i];
            for (int r = i + 1; r < n; r++) { double fct = m[r, i] / m[i, i]; for (int cidx = i; cidx < n; cidx++) m[r, cidx] -= fct * m[i, cidx]; }
        }
        return det;
    }
    public static double[,]? Inverse(double[,] a, int n)
    {
        var m = new double[n, 2 * n]; for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) m[i, j] = a[i, j]; m[i, n + i] = 1; }
        for (int i = 0; i < n; i++)
        {
            int piv = i; for (int r = i + 1; r < n; r++) if (Math.Abs(m[r, i]) > Math.Abs(m[piv, i])) piv = r;
            if (Math.Abs(m[piv, i]) < 1e-300) return null;
            for (int cidx = 0; cidx < 2 * n; cidx++) (m[i, cidx], m[piv, cidx]) = (m[piv, cidx], m[i, cidx]);
            double dv = m[i, i]; for (int cidx = 0; cidx < 2 * n; cidx++) m[i, cidx] /= dv;
            for (int r = 0; r < n; r++) if (r != i) { double fct = m[r, i]; for (int cidx = 0; cidx < 2 * n; cidx++) m[r, cidx] -= fct * m[i, cidx]; }
        }
        var inv = new double[n, n]; for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) inv[i, j] = m[i, n + j]; return inv;
    }
    static double[] LeastSquares(double[,] X, double[] y, int n, int p)
    {
        var A = new double[p, p]; var b = new double[p];
        for (int k = 0; k < n; k++) for (int i = 0; i < p; i++) { b[i] += X[k, i] * y[k]; for (int j = 0; j < p; j++) A[i, j] += X[k, i] * X[k, j]; }
        var inv = Inverse(A, p)!; var beta = new double[p];
        for (int i = 0; i < p; i++) for (int j = 0; j < p; j++) beta[i] += inv[i, j] * b[j];
        return beta;
    }
}
