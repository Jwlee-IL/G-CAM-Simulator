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
import math
import os
import trap_ref


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


def rasterize(events, tau=trap_ref.TAU_SAMPLES, adc_per_kev=trap_ref.ADC_PER_KEV, tail_pad=128):
    """Sum every event's exponential pulse into one ADC waveform (list of ints).

    Bit-identical to summing full-length trap_ref.exp_pulse/add_pulse contributions: each pulse is
    walked from its arrival until its rounded sample first hits 0 (monotone decay => all later
    samples are 0 too). tail_pad leaves room after the last arrival for its flat top to form."""
    if not events:
        return []
    length = max(s for s, _ in events) + tail_pad
    wave = [0] * length
    for n0, e_kev in events:
        a = e_kev * adc_per_kev
        t = n0
        while t < length:
            val = int(round(a * math.exp(-(t - n0) / tau)))
            if val == 0 and t > n0:
                break                       # decayed below 0.5 ADC — rest is exactly 0
            wave[t] += val
            t += 1
    return wave


def calibrate_flat_per_kev(rise=trap_ref.RISE, flat=trap_ref.FLAT, m_q8=trap_ref.M_Q8,
                           tau=trap_ref.TAU_SAMPLES, adc_per_kev=trap_ref.ADC_PER_KEV, ref_kev=662.0):
    """Flat-top height the shaper produces per keV, from ONE isolated reference pulse (so recovered
    energy = flat_top / this). Uses the same integer reference as the RTL, so it IS the RTL gain."""
    n0 = 4 * rise
    pulse = trap_ref.exp_pulse(n0 + rise + flat + 8 * int(math.ceil(tau)) + 8, n0, ref_kev, tau)
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
