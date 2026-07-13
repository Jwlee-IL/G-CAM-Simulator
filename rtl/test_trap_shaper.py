"""cocotb testbench: drive trapezoidal_shaper.sv and assert it matches the integer
reference bit-for-bit on a scintillation pulse (and a piled-up pair).

Run via `python rtl/run_cocotb.py` (uses the cocotb Icarus runner). This is the first
cocotb co-sim in the project — it drives the RTL directly instead of the file-I/O TB.
"""
import cocotb
from cocotb.clock import Clock
from cocotb.triggers import RisingEdge, Timer
import trap_ref


async def _run(dut, samples):
    cocotb.start_soon(Clock(dut.clk, 10, units="ns").start())
    dut.rst.value = 1
    dut.valid.value = 0
    dut.sample.value = 0
    for _ in range(3):
        await RisingEdge(dut.clk)
    dut.rst.value = 0
    await RisingEdge(dut.clk)

    got = []
    dut.valid.value = 1
    for v in samples:
        dut.sample.value = int(v)
        await RisingEdge(dut.clk)
        await Timer(1, units="ns")           # let the NBA update settle before reading
        got.append(int(dut.shaped.value.to_signed()))
    return got


def _best_offset(got, ref, maxoff=6):
    """The registered output lags the reference by a fixed latency; find it (0..maxoff)."""
    best, best_bad = 0, 10**9
    for off in range(maxoff + 1):
        bad = sum(1 for i in range(len(ref) - off) if got[i + off] != ref[i])
        if bad < best_bad:
            best_bad, best = bad, off
    return best, best_bad


# Contractual latency: the direct shaper is +0 (its output is s[n]); the pipelined one is +3.
EXPECTED_LATENCY = {"trapezoidal_shaper": 0, "trapezoidal_shaper_pl": 3}


def _check(dut, got, ref, tag):
    off, bad = _best_offset(got, ref)
    assert bad == 0, f"{tag}: {bad} mismatches vs reference (best latency {off})"
    exp = EXPECTED_LATENCY.get(getattr(dut, "_name", ""))
    if exp is not None:
        assert off == exp, f"{tag}: latency {off} != contractual {exp} for {dut._name}"


@cocotb.test()
async def single_pulse_matches_reference(dut):
    samples = trap_ref.exp_pulse(120, 20, 662.0)
    got = await _run(dut, samples)
    _check(dut, got, trap_shape_from_dut(dut, samples), "single pulse")


@cocotb.test()
async def piled_pulses_match_reference(dut):
    samples = trap_ref.exp_pulse(160, 20, 662.0)
    trap_ref.add_pulse(samples, 40, 1332.0)     # a second pulse on the tail (pile-up)
    got = await _run(dut, samples)
    _check(dut, got, trap_shape_from_dut(dut, samples), "piled pulses")


def trap_shape_from_dut(dut, samples):
    """Reference using the SAME parameters the RTL was built with (read from the DUT)."""
    rise = int(dut.RISE.value)
    flat = int(dut.FLAT.value)
    m_q8 = int(dut.M_Q8.value)
    return trap_ref.trap_shape(samples, rise, flat, m_q8)


@cocotb.test()
async def mc_event_stream_matches_reference(dut):
    """Drive the RTL shaper from the C# Monte Carlo event stream (rtl/event_stream.txt): the real
    crystal-Compton deposit spectrum (photopeak + continuum) with a Poisson arrival overlay,
    rasterized into an ADC waveform. Assert the RTL processes it bit-for-bit like the integer
    reference — the closed MC->RTL loop. (Run `montecarlo eventstream <cfg>` first to write the file.)"""
    import os
    import event_stream
    if not os.path.exists(os.path.join(os.path.dirname(__file__), "event_stream.txt")):
        raise cocotb.result.SkipTest("no event_stream.txt — run `montecarlo eventstream <cfg>` first")

    events, meta = event_stream.read_stream()
    # Cap the arrival span to bound Icarus sim time; the FULL stream drives event_stream_study.py
    # (pure-python reference, which is bit-exact to this RTL).
    cap = 30000
    events = [(s, e) for (s, e) in events if s < cap]
    wave = event_stream.rasterize(events)
    assert wave, "empty MC stream"
    assert max(wave) < 32768, f"waveform peak {max(wave)} overflows the 16-bit ADC input at this rate"

    got = await _run(dut, wave)
    _check(dut, got, trap_shape_from_dut(dut, wave),
           f"MC stream ({len(events)} events, {len(wave)} samples, rate={meta.get('count_rate_cps','?')})")
