"""cocotb testbench: drive crrc_shaper.sv and assert it matches the integer CR-RC reference bit-for-bit,
and that the semi-Gaussian PEAK is proportional to deposited energy (the CR-RC companion to the trapezoid).

Run via `python rtl/run_cocotb.py`.
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
        await Timer(1, units="ns")
        got.append(int(dut.shaped.value.to_signed()))
    return got


def _best_offset(got, ref, maxoff=4):
    best, best_bad = 0, 10 ** 9
    for off in range(maxoff + 1):
        bad = sum(1 for i in range(len(ref) - off) if got[i + off] != ref[i])
        if bad < best_bad:
            best_bad, best = bad, off
    return best, best_bad


def _ref(dut, samples):
    a = int(dut.A_Q16.value)
    k = int(dut.K_Q16.value)
    order = int(dut.ORDER.value)
    # The reference reuses the DUT's constants, so pin them to the math (and so to C#'s Waveform): otherwise a
    # drifted constant stays self-consistent and passes - the 53667-vs-53656 bug of commit c884d53.
    assert a == trap_ref.CRRC_A_Q16, f"A_Q16 {a} != round(exp(-1/tau)*65536) = {trap_ref.CRRC_A_Q16}"
    assert k == trap_ref.CRRC_K_Q16, f"K_Q16 {k} != round(65536/tau_s) = {trap_ref.CRRC_K_Q16}"
    return trap_ref.crrc_int(samples, a, k, order)


@cocotb.test()
async def single_pulse_matches_reference(dut):
    samples = trap_ref.exp_pulse(120, 20, 662.0)
    got = await _run(dut, samples)
    off, bad = _best_offset(got, _ref(dut, samples))
    assert bad == 0, f"single pulse: {bad} mismatches vs CR-RC reference (latency {off})"


@cocotb.test()
async def piled_pulses_match_reference(dut):
    samples = trap_ref.exp_pulse(160, 20, 662.0)
    trap_ref.add_pulse(samples, 40, 1332.0)
    got = await _run(dut, samples)
    off, bad = _best_offset(got, _ref(dut, samples))
    assert bad == 0, f"piled pulses: {bad} mismatches vs CR-RC reference (latency {off})"


@cocotb.test()
async def peak_is_proportional_to_energy(dut):
    p400 = max(await _run(dut, trap_ref.exp_pulse(120, 20, 400.0)))
    p800 = max(await _run(dut, trap_ref.exp_pulse(120, 20, 800.0)))
    # doubling the energy doubles the CR-RC peak (linear filter), within a few % for integer rounding.
    assert abs(p800 / p400 - 2.0) < 0.05, f"CR-RC peak should be linear in energy: 400->{p400}, 800->{p800}"
