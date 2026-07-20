"""Integer reference model of trapezoidal_shaper.sv (bit-exact) + stimulus helpers.

The SV uses a signed arithmetic right shift for the /256; Python's >> on ints is also an
arithmetic (floor) shift, so this reference matches the RTL bit-for-bit. Shared by the cocotb
test (RTL vs reference) and the study/plot (so the plotted curves ARE the RTL behaviour).
"""
import math

# Default operating point: sample period Ts, scintillation decay tau (in SAMPLES).
TAU_SAMPLES = 5.0
RISE, FLAT = 10, 8
M = 1.0 / (math.exp(1.0 / TAU_SAMPLES) - 1.0)      # pole-zero deconvolution constant
M_Q8 = round(M * 256)                               # 1156
ADC_PER_KEV = 4.0

# --- realistic front-end parameters (used by the MC-stream rasterizer, not the pure shaper unit tests) ---
TAU_RISE_SAMPLES = 1.0     # finite pulse rise ~ 8-10 ns: the fastest an ADC can cleanly sample (BW ~39 MHz <
                           # Nyquist at 125 MSPS, ~3 samples on the rising edge). Faster aliases; slower over-
                           # suppresses the peak (ballistic deficit). Bounded BELOW by the anti-alias / Nyquist limit.
NOISE_KEV = 3.0            # white electronic noise floor, keV-equivalent RMS at the ADC input (ENC)
ADC_MAX = 32767            # 16-bit signed ADC full scale — the shaper input port width (pile-up stacks clip)
INTRINSIC_FWHM = 0.06      # scintillator+SiPM photostatistical resolution (FWHM fraction) at INTRINSIC_REF_KEV
INTRINSIC_REF_KEV = 662.0  # ... scaling as 1/sqrt(E) (Poisson photoelectron statistics). GAGG ~6-9% @ 662.


def trap_shape(samples, rise=RISE, flat=FLAT, m_q8=M_Q8):
    """s[n] sequence for the recursive trapezoidal filter (matches trapezoidal_shaper.sv)."""
    L, KL = rise + flat, rise + (rise + flat)
    dl = [0] * KL
    p = s = 0
    out = []
    for v in samples:
        dkl = v - dl[rise - 1] - dl[L - 1] + dl[KL - 1]
        p = p + dkl
        r = p + ((dkl * m_q8) >> 8)                 # arithmetic shift, as SV >>>
        s = s + r
        out.append(s)
        for i in range(KL - 1, 0, -1):
            dl[i] = dl[i - 1]
        dl[0] = v
    return out


def blr(xs, gate=4096, frac=12):
    """Integer reference for baseline_restorer.sv (bit-exact). A gated leaky integrator tracks the DC
    baseline only in quiet regions (|x-base| < gate) and subtracts it, cancelling the shaper's pole-zero
    baseline walk. Python `>>` on ints is an arithmetic (floor) shift, matching the SV signed `>>>`; the
    estimate is carried in Q(frac) so the small per-sample step does not round away."""
    base_acc = 0
    out = []
    for x in xs:
        base = base_acc >> frac
        diff = x - base
        out.append(x - base)                 # registered output uses the PRE-update baseline
        if -gate < diff < gate:
            base_acc += diff
    return out


# CR-RC^n shaper fixed-point constants (Q16). A = exp(-1/tau) deconvolves the exp tail (pole-zero);
# K = 1/tau_s is the single-pole RC low-pass gain. Defaults: tau=5 (matches the trapezoid), tau_s=2.5, order 4.
CRRC_ORDER = 4
CRRC_A_Q16 = round(math.exp(-1.0 / TAU_SAMPLES) * 65536)   # 53656
CRRC_K_Q16 = round((1.0 / 2.5) * 65536)                     # 26214


def crrc_int(samples, a_q16=CRRC_A_Q16, k_q16=CRRC_K_Q16, order=CRRC_ORDER):
    """Integer CR-RC^order reference, bit-exact to crrc_shaper.sv: deconvolve the exp tail (imp = x - A·x[-1])
    then `order` single-pole RC low-passes (acc += (u-acc)·K, all Q16), each stage feeding the next this
    sample. Python `>>` is arithmetic (floor), matching the SV signed `>>>`. Returns the last stage (∝ energy)."""
    prev = 0
    acc = [0] * order
    out = []
    for x in samples:
        imp = x - ((a_q16 * prev) >> 16)
        prev = x
        u = imp
        for i in range(order):
            acc[i] = acc[i] + (((u - acc[i]) * k_q16) >> 16)
            u = acc[i]
        out.append(u)
    return out


def exp_pulse(n, n0, amp_kev, tau=TAU_SAMPLES):
    """One scintillation pulse: instantaneous rise at n0, exponential decay tau (samples). Used by the pure
    shaper unit tests (filter correctness); the realistic front-end uses biexp_pulse via the rasterizer."""
    w = [0] * n
    a = amp_kev * ADC_PER_KEV
    for t in range(n0, n):
        w[t] += int(round(a * math.exp(-(t - n0) / tau)))
    return w


def biexp_pulse(n, n0, amp_kev, tau=TAU_SAMPLES, tau_rise=TAU_RISE_SAMPLES, adc_per_kev=ADC_PER_KEV):
    """One scintillation + front-end pulse with a FINITE rise: a*(exp(-t/tau) - exp(-t/tau_rise)). The
    tail is a*exp(-t/tau) so the shaper's pole-zero and the energy calibration are unchanged; the rise
    (tau_rise < tau) is the realistic part — it introduces a small ballistic deficit in the flat top.
    Returns a float array (the caller adds noise, clips and quantizes)."""
    w = [0.0] * n
    a = amp_kev * adc_per_kev
    for t in range(n0, n):
        dt = t - n0
        w[t] = a * (math.exp(-dt / tau) - math.exp(-dt / tau_rise))
    return w


def add_pulse(w, n0, amp_kev, tau=TAU_SAMPLES):
    a = amp_kev * ADC_PER_KEV
    for t in range(n0, len(w)):
        w[t] += int(round(a * math.exp(-(t - n0) / tau)))
    return w
