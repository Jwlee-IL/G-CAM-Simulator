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


@cocotb.test()
async def single_pulse_matches_reference(dut):
    samples = trap_ref.exp_pulse(120, 20, 662.0)
    got = await _run(dut, samples)
    ref = trap_shape_from_dut(dut, samples)
    off, bad = _best_offset(got, ref)
    assert bad == 0, f"single pulse: {bad} mismatches vs reference (latency {off})"


@cocotb.test()
async def piled_pulses_match_reference(dut):
    samples = trap_ref.exp_pulse(160, 20, 662.0)
    trap_ref.add_pulse(samples, 40, 1332.0)     # a second pulse on the tail (pile-up)
    got = await _run(dut, samples)
    ref = trap_shape_from_dut(dut, samples)
    off, bad = _best_offset(got, ref)
    assert bad == 0, f"piled pulses: {bad} mismatches vs reference (latency {off})"


def trap_shape_from_dut(dut, samples):
    """Reference using the SAME parameters the RTL was built with (read from the DUT)."""
    rise = int(dut.RISE.value)
    flat = int(dut.FLAT.value)
    m_q8 = int(dut.M_Q8.value)
    return trap_ref.trap_shape(samples, rise, flat, m_q8)
