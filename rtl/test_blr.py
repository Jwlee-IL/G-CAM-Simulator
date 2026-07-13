"""cocotb testbench: drive baseline_restorer.sv with the SHAPED Monte Carlo event stream and assert it
(1) matches the integer reference bit-for-bit and (2) actually removes the shaper's pole-zero baseline
walk (theme 27) — the raw shaped baseline drifts far negative over the train, the BLR output stays near
zero while preserving the pulse flat tops.

Run via `python rtl/run_cocotb.py`. Needs rtl/event_stream.txt (`montecarlo eventstream <cfg>`).
"""
import os
import cocotb
from cocotb.clock import Clock
from cocotb.triggers import RisingEdge, Timer
import trap_ref
import event_stream

CAP = 200_000     # samples of the stream to run (enough to build a clear baseline walk); ~11 s in Icarus


async def _run(dut, samples):
    cocotb.start_soon(Clock(dut.clk, 10, units="ns").start())
    dut.rst.value = 1
    dut.valid.value = 0
    dut.x.value = 0
    for _ in range(3):
        await RisingEdge(dut.clk)
    dut.rst.value = 0
    await RisingEdge(dut.clk)

    got = []
    dut.valid.value = 1
    for v in samples:
        dut.x.value = int(v)
        await RisingEdge(dut.clk)
        await Timer(1, units="ns")           # let the NBA update settle before reading
        got.append(int(dut.y.value.to_signed()))
    return got


def _best_offset(got, ref, maxoff=4):
    """The registered BLR output lags the reference by a fixed latency; find it."""
    best, best_bad = 0, 10 ** 9
    for off in range(maxoff + 1):
        bad = sum(1 for i in range(len(ref) - off) if got[i + off] != ref[i])
        if bad < best_bad:
            best_bad, best = bad, off
    return best, best_bad


@cocotb.test()
async def blr_matches_reference_and_removes_walk(dut):
    if not os.path.exists(os.path.join(os.path.dirname(__file__), "event_stream.txt")):
        raise cocotb.result.SkipTest("no event_stream.txt — run `montecarlo eventstream <cfg>` first")

    events, _ = event_stream.read_stream()
    events = [(s, e) for (s, e) in events if s < CAP]
    wave = event_stream.rasterize(events)
    shaped = trap_ref.trap_shape(wave)                 # the shaper output, WITH the pole-zero baseline walk

    gate = int(dut.GATE.value)
    frac = int(dut.FRAC.value)
    ref = trap_ref.blr(shaped, gate, frac)

    got = await _run(dut, shaped)
    off, bad = _best_offset(got, ref)
    assert bad == 0, f"{bad} mismatches vs BLR reference (best latency {off})"

    # (2) the walk is real in the raw shaped stream, and the BLR flattens it while keeping the flat tops.
    raw_min, blr_min, blr_max = min(shaped), min(got), max(got)
    assert raw_min < -15000, f"expected a clear raw baseline walk, got min {raw_min}"
    assert abs(blr_min) < abs(raw_min) / 3, f"BLR did not flatten the baseline: raw min {raw_min}, blr min {blr_min}"
    assert blr_max > 100000, f"BLR should preserve pulse flat tops, got max {blr_max}"
