"""Exact C#/Python/RTL CR-RC contract; zero accepting-edge sequence latency.
The expected parameters come from fixture math, never from DUT readback.
"""
import json
import os
import random
import cocotb
from cocotb.clock import Clock
from cocotb.triggers import RisingEdge, FallingEdge, Timer
import trap_ref
from crrc_contract import fixtures


def _contract(dut):
    name = os.environ.get("GCAM_CRRC_FIXTURE", "legacy")
    spec = fixtures()[name]
    fractional = int(os.environ.get("GCAM_CRRC_F", "0"))
    for key, expected in {"ORDER": spec["order"], "A_Q16": spec["a"], "K_Q16": spec["k"],
                          "F": fractional, "Q": 16, "WIN": 16, "WACC": 48}.items():
        assert int(getattr(dut, key).value) == expected, f"{name}: {key} differs from independent math"
    return name, spec, fractional


async def _run(dut, samples, gaps=False):
    clock = Clock(dut.clk, 8, unit="ns")
    clock.start()
    dut.rst.value = 1
    dut.valid.value = 0
    dut.sample.value = 0
    for _ in range(3):
        await RisingEdge(dut.clk)
    await FallingEdge(dut.clk)
    dut.rst.value = 0
    got = []
    for index, value in enumerate(samples):
        if gaps and index % 7 == 0:
            dut.valid.value = 0
            dut.sample.value = -32768  # must not advance prev or any RC state
            await RisingEdge(dut.clk)
            await Timer(1, unit="ns")
            assert int(dut.shaped.value.to_signed()) == (got[-1] if got else 0)
            await FallingEdge(dut.clk)
        dut.valid.value = 1
        dut.sample.value = value
        await RisingEdge(dut.clk)
        await Timer(1, unit="ns")
        got.append(int(dut.shaped.value.to_signed()))
        await FallingEdge(dut.clk)
    clock.stop()
    return got


def _ref(dut, samples):
    _, spec, fractional = _contract(dut)
    return trap_ref.crrc_int(samples, spec["a"], spec["k"], spec["order"], fractional)


def _pulse(spec, energy, n=1024, n0=100):
    return [round(x) for x in trap_ref.biexp_pulse(n, n0, energy, spec["tail"], spec["rise"], 8191 / 2000)]


@cocotb.test()
async def finite_rise_energies_match_reference(dut):
    _, spec, _ = _contract(dut)
    samples = sum((_pulse(spec, energy) for energy in (32, 32.1, 122, 662)), [])
    # Retain the original piled-pulse regression, now with this fixture's finite-rise response.
    first, second = _pulse(spec, 662), _pulse(spec, 1332, n0=120)
    samples += [x + y for x, y in zip(first, second)]
    # Concatenation deliberately retains history between pulses; identical reference initial state.
    assert await _run(dut, samples) == _ref(dut, samples)


@cocotb.test()
async def near_threshold_sweep_matches_reference(dut):
    _, spec, _ = _contract(dut)
    samples = sum((_pulse(spec, 27 + index * .25) for index in range(41)), [])
    assert await _run(dut, samples) == _ref(dut, samples)


@cocotb.test()
async def signed_full_scale_noise_and_valid_gaps_match_reference(dut):
    _contract(dut)
    rng = random.Random(20261002)
    samples = [32767, -32768] * 128 + [32767] * 256 + [-32768] * 256
    samples += [rng.randrange(-32768, 32768) for _ in range(1024)] + [0] * 1024
    assert await _run(dut, samples, gaps=True) == _ref(dut, samples)


@cocotb.test()
async def legacy_golden_and_proportionality_are_preserved(dut):
    name, spec, fractional = _contract(dut)
    if name != "legacy":
        samples = _pulse(spec, 662)
        assert await _run(dut, samples) == await _run(dut, samples)
        return
    # Existing 400->800 tolerance is retained, never loosened. This uses an instantaneous-tail stimulus.
    p400 = max(await _run(dut, trap_ref.exp_pulse(1024, 20, 400, spec["tail"])))
    p800 = max(await _run(dut, trap_ref.exp_pulse(1024, 20, 800, spec["tail"])))
    assert abs(p800 / p400 - 2) < .05
    if name == "legacy" and fractional == 0:
        got = await _run(dut, trap_ref.exp_pulse(80, 20, 662))
        assert max(got) == 304 and got[35] == 23


@cocotb.test(skip=not os.environ.get("GCAM_CRRC_VECTORS"))
async def csharp_vectors_match_python_and_rtl(dut):
    name, spec, fractional = _contract(dut)
    root = os.environ.get("GCAM_CRRC_VECTORS")
    if not root:
        raise RuntimeError("C# vectors required: run exporter then use --csharp-vectors")
    with open(os.path.join(root, f"{name}-f{fractional}.json"), encoding="utf-8-sig") as stream:
        vector = json.load(stream)
    assert (vector["A"], vector["K"], vector["Order"], vector["F"]) == (spec["a"], spec["k"], spec["order"], fractional)
    assert vector["Expected"] == _ref(dut, vector["Samples"])
    assert await _run(dut, vector["Samples"], gaps=True) == vector["Expected"]
