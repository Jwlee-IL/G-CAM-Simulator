"""Digital pulse shapers for the comparison study — CR-RC^n (semi-Gaussian), trapezoidal, and a finite
cusp — all operating on the same realistic ADC waveform (event_stream.rasterize).

Each shaper turns the exp-tail pulse train into an output whose amplitude ∝ deposited energy. They differ
in their NOISE weighting (how well they average the electronic-noise floor) and their SUPPORT (how badly
pile-up corrupts them at high rate):

  * CR-RC^n : deconvolve the tail, then n single-pole RC low-passes -> a Gamma-shaped semi-Gaussian. Simple,
              moderate noise, moderate width.
  * trapezoid: flat-top (trap_ref.trap_shape) -> ballistic-deficit immune (accurate energy despite variable
              charge-collection time), at the cost of a little noise AND extra width (the flat top).
  * cusp    : deconvolve, then a symmetric sinh-cusp FIR weighting -> near-OPTIMAL noise (lowest ENC for
              series+parallel noise) at a given width.

Pile-up follows the total SUPPORT WIDTH (any shape), so the flat-top trapezoid — being widest here — piles up
MOST; its flat top is a ballistic-deficit feature, not a rate advantage (see shaper_compare.py).

The trapezoid reads a flat top; CR-RC and cusp read the output PEAK. All are calibrated per keV the same way.
"""
import math
import trap_ref


def deconv(x, tau=trap_ref.TAU_SAMPLES):
    """Pole-zero deconvolve the exponential fall (tau samples): imp[n] = x[n] - exp(-1/tau)*x[n-1] turns a
    single exp-tail pulse back into a charge impulse (the common input the CR-RC / cusp weightings act on)."""
    a = math.exp(-1.0 / tau)
    out = [0.0] * len(x)
    prev = 0.0
    for n in range(len(x)):
        out[n] = x[n] - a * prev
        prev = x[n]
    return out


def crrc(x, order=4, tau_s=2.5, tau=trap_ref.TAU_SAMPLES):
    """CR-RC^order semi-Gaussian: deconvolve the tail, then `order` single-pole RC low-passes (time
    constant tau_s). Peaks ~order*tau_s after the event; peaking time ~ that. Peak height ∝ energy."""
    y = deconv(x, tau)
    k = 1.0 / tau_s
    for _ in range(order):
        z = [0.0] * len(y)
        acc = 0.0
        for n in range(len(y)):
            acc += (y[n] - acc) * k
            z[n] = acc
        y = z
    return y


def cusp_weights(half, tau_c):
    """Symmetric sinh-cusp FIR weighting (rise sinh(k/tau_c) over `half`, mirrored) — the near-optimal
    shape for series+parallel noise. Longer half = longer shaping (better noise, worse pile-up)."""
    w = [math.sinh(k / tau_c) for k in range(half + 1)]
    return w + w[-2::-1]


def cusp(x, half=10, tau_c=4.0, tau=trap_ref.TAU_SAMPLES):
    """Finite cusp: deconvolve the tail, then FIR-convolve with the sinh-cusp weighting. Output peak ∝ energy."""
    imp = deconv(x, tau)
    w = cusp_weights(half, tau_c)
    L = len(w)
    n_len = len(imp)
    out = [0.0] * n_len
    for n in range(n_len):
        s = 0.0
        base = n
        for k in range(L):
            m = base - k
            if 0 <= m < n_len:
                s += w[k] * imp[m]
        out[n] = s
    return out


def peak_in(shaped, n0, lo_off, hi_off):
    """Peak of a peaking shaper's output in [n0+lo_off, n0+hi_off] (for CR-RC / cusp energy read)."""
    lo = max(0, n0 + lo_off)
    hi = min(len(shaped), n0 + hi_off)
    return max(shaped[lo:hi]) if hi > lo else 0.0
