"""Read a GCAM MC event stream (rtl/event_stream.txt, written by `montecarlo eventstream`) and
rasterize it into the ADC sample waveform the trapezoidal shaper consumes.

Each event is one scintillation pulse: an instantaneous rise at its arrival sample, exponential
decay tau (samples), amplitude = deposited_keV * ADC_PER_KEV — the SAME pulse model as
trap_ref.exp_pulse, so a waveform built here drives the RTL bit-for-bit identically to the
reference. Events overlap in the waveform exactly as they pile up in real hardware; the shaper's
job is to recover each event's flat-top height (= deposited energy) despite that overlap.

Shared by the cocotb test (RTL vs reference on the MC stream) and event_stream_study.py (recovery
and pile-up analysis). The rasterizer truncates each pulse at the sample where its rounded
contribution first reaches 0 — those samples add nothing, so the waveform is bit-identical to a
full-length exp_pulse while keeping the build O(events x pulse_support) instead of O(events x length).
"""
import json
import math
import os
import random
import trap_ref

_ADC_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "samples", "adc")
DEFAULT_ADC = "ad9648"


def load_adc(name=DEFAULT_ADC):
    """Load an ADC preset (samples/adc/<name>.json) and derive the model quantities from datasheet specs:
    the signed full-scale code (from bits), the codes-per-keV gain (full scale mapped to fullScaleKeV so the
    lines sit well up the range), and the ADC's own input-referred noise in codes (from ENOB via
    SNR = 6.02*ENOB + 1.76 dB, ENOB taken as the SINAD-equivalent broadband ADC noise). The analog/preamp
    noise (noise_kev) is a SEPARATE, usually larger term added in quadrature by the rasterizer."""
    with open(os.path.join(_ADC_DIR, name + ".json")) as f:
        d = json.load(f)
    bits = int(d["bits"])
    adc_max = (1 << (bits - 1)) - 1                       # signed full scale, e.g. 14-bit -> 8191
    adc_per_kev = adc_max / float(d["fullScaleKeV"])
    snr_db = 6.02 * float(d["enob"]) + 1.76
    adc_noise_codes = (adc_max / math.sqrt(2.0)) / (10.0 ** (snr_db / 20.0))
    return {"name": d["name"], "bits": bits, "adc_max": adc_max, "adc_per_kev": adc_per_kev,
            "adc_noise_codes": adc_noise_codes, "full_scale_kev": float(d["fullScaleKeV"]),
            "sample_rate_msps": d.get("sampleRateMsps")}


ADC = load_adc()   # the active ADC preset (module default); swap by passing adc=load_adc("...") to rasterize


def read_stream(path=None):
    """Return (events, meta): events = [(arrival_sample, energy_keV), ...] sorted by arrival;
    meta = the header key/values (count_rate_cps, adc_sample_rate_hz, ...)."""
    path = path or os.path.join(os.path.dirname(os.path.abspath(__file__)), "event_stream.txt")
    events, meta = [], {}
    with open(path) as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            if line.startswith("#"):
                for tok in line.lstrip("#").split():
                    if "=" in tok:
                        k, v = tok.split("=", 1)
                        meta[k] = v
                continue
            s, e = line.split()
            events.append((int(s), float(e)))
    events.sort(key=lambda ev: ev[0])
    return events, meta


def rasterize(events, tau=trap_ref.TAU_SAMPLES, tau_rise=trap_ref.TAU_RISE_SAMPLES,
              adc=None, noise_kev=trap_ref.NOISE_KEV, intrinsic_fwhm=trap_ref.INTRINSIC_FWHM,
              intrinsic_ref_kev=trap_ref.INTRINSIC_REF_KEV, seed=1, tail_pad=128):
    """Build the realistic ADC waveform for the event stream (list of ints). Real front-end effects, so
    the shaper is not fed an idealized noiseless step train:

      * per-event INTRINSIC resolution — each pulse's amplitude fluctuates by the scintillator+SiPM
        photostatistics (intrinsic_fwhm FWHM at intrinsic_ref_kev, scaling 1/sqrt(E));
      * FINITE RISE — a bi-exponential pulse (rise tau_rise, fall tau) → ballistic deficit;
      * NOISE — the analog/preamp floor (noise_kev keV-equivalent RMS) in quadrature with the chosen ADC's
        OWN input-referred noise (from its ENOB); the floor the shaper fights;
      * ADC CLIP + quantize to the ADC's signed full scale (from its bit depth), so pile-up stacks saturate.

    `adc` is an ADC preset dict from load_adc() (defaults to the module ADC = AD9648 14-bit); it sets the
    codes-per-keV gain, the full-scale clip, and the ADC's own noise. Set intrinsic_fwhm=0, noise_kev=0,
    tau_rise=0 for the old ideal shape. `seed` fixes the amplitude and sample-noise RNG streams."""
    if not events:
        return []
    a_adc = adc or ADC
    adc_per_kev = a_adc["adc_per_kev"]
    adc_max = a_adc["adc_max"]
    length = max(s for s, _ in events) + tail_pad
    wave = [0.0] * length
    amp_rng = random.Random(seed + 777)     # per-event amplitude (intrinsic resolution) — its own stream
    inv2355 = 1.0 / 2.3548
    for n0, e_kev in events:
        e_eff = e_kev
        if intrinsic_fwhm > 0.0 and e_kev > 0.0:
            # relative sigma grows as 1/sqrt(E): rel_FWHM(E) = intrinsic_fwhm * sqrt(ref/E).
            rel_sigma = intrinsic_fwhm * inv2355 * math.sqrt(intrinsic_ref_kev / e_kev)
            e_eff = e_kev * (1.0 + amp_rng.gauss(0.0, rel_sigma))
            if e_eff < 0.0:
                e_eff = 0.0
        a = e_eff * adc_per_kev
        # bounded support: the fall term a*exp(-dt/tau) drops below ~0.5 ADC by here (rise term is smaller)
        support = int(math.ceil(tau * math.log(2.0 * abs(a) + 2.0))) + 4
        end = min(length, n0 + support)
        for t in range(n0, end):
            dt = t - n0
            fall = math.exp(-dt / tau)
            rise = math.exp(-dt / tau_rise) if tau_rise > 0.0 else 0.0
            wave[t] += a * (fall - rise)

    # per-sample noise = analog/preamp (noise_kev -> codes) in quadrature with the ADC's own input-referred noise
    sigma = math.sqrt((noise_kev * adc_per_kev) ** 2 + a_adc["adc_noise_codes"] ** 2)
    noise_rng = random.Random(seed)
    out = [0] * length
    for i in range(length):
        v = wave[i] + (noise_rng.gauss(0.0, sigma) if sigma > 0.0 else 0.0)
        if v > adc_max:
            v = adc_max
        elif v < -adc_max:
            v = -adc_max
        out[i] = int(round(v))
    return out


def calibrate_flat_per_kev(rise=trap_ref.RISE, flat=trap_ref.FLAT, m_q8=trap_ref.M_Q8,
                           tau=trap_ref.TAU_SAMPLES, tau_rise=trap_ref.TAU_RISE_SAMPLES,
                           adc=None, ref_kev=662.0):
    """Flat-top height the shaper produces per keV, from ONE isolated NOISELESS reference pulse (so
    recovered energy = flat_top / this). Uses the SAME finite-rise pulse model and the SAME ADC gain as the
    rasterizer, so the gain already includes the ballistic deficit; noise is excluded (deterministic gain)."""
    adc_per_kev = (adc or ADC)["adc_per_kev"]
    n0 = 4 * rise
    length = n0 + rise + flat + 8 * int(math.ceil(tau)) + 8
    pulse = [int(round(v)) for v in trap_ref.biexp_pulse(length, n0, ref_kev, tau, tau_rise, adc_per_kev)]
    shaped = trap_ref.trap_shape(pulse, rise, flat, m_q8)
    return max(shaped) / ref_kev


def flat_top(shaped, n0, rise=trap_ref.RISE, flat=trap_ref.FLAT, latency=0, guard=4):
    """Flat-top height of the pulse that arrived at n0, measured RELATIVE to the local pre-pulse
    baseline — i.e. peak over [n0+rise, n0+rise+flat] minus the level just before the ramp starts.

    The relative read is what real trapezoidal DAQs do (baseline restoration): the Q8-quantized
    pole-zero constant leaves a tiny residual per pulse, so over a long pulse train the absolute
    baseline walks by tens of thousands of ADC counts (an MC->RTL-stream finding — a single synthetic
    pulse never shows it). Subtracting the local baseline recovers the true height and isolates the
    genuine pile-up distortion (a neighbour's flat top raising the pedestal) from the slow walk."""
    lo = max(0, n0 + rise + latency - guard)
    hi = min(len(shaped), n0 + rise + flat + latency + guard + 1)
    peak = max(shaped[lo:hi]) if hi > lo else 0
    # shaped[n0+latency-1] is the last output BEFORE this pulse's ramp = local baseline. If the pulse
    # arrives at sample 0 there is no pre-ramp sample (shaped[0] already contains it), and there is no
    # accumulated walk yet anyway, so the baseline is 0.
    bi = n0 + latency - 1
    base = shaped[bi] if bi >= 0 else 0
    return peak - base
