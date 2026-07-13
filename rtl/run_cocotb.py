"""Build + run the cocotb testbench for trapezoidal_shaper.sv with the Icarus runner.
Usage (from rtl/):  python run_cocotb.py
Exits non-zero if any cocotb test fails.
"""
import os
import sys
from cocotb_tools.runner import get_runner

here = os.path.dirname(os.path.abspath(__file__))
os.chdir(here)

# Both the direct and the pipelined shaper are verified against the SAME integer reference
# (the pipeline is bit-exact, just +3 samples of latency — the test's offset search handles it).
for top, src in [("trapezoidal_shaper", "trapezoidal_shaper.sv"),
                 ("trapezoidal_shaper_pl", "trapezoidal_shaper_pl.sv")]:
    print(f"\n===== {top} =====")
    runner = get_runner("icarus")
    runner.build(
        sources=[os.path.join(here, src)],
        hdl_toplevel=top,
        build_args=["-g2012"],
        parameters={"M_Q8": 1156, "RISE": 10, "FLAT": 8},   # match trap_ref defaults (tau=5 samples)
        always=True,
    )
    results = runner.test(
        hdl_toplevel=top,
        test_module="test_trap_shaper",
        test_dir=here,
    )
    print(f"results xml: {results}")

# Baseline restorer: driven by the SHAPED MC stream, checked bit-exact + that it removes the pole-zero walk.
print("\n===== baseline_restorer =====")
blr = get_runner("icarus")
blr.build(
    sources=[os.path.join(here, "baseline_restorer.sv")],
    hdl_toplevel="baseline_restorer",
    build_args=["-g2012"],
    parameters={"WACC": 32, "GATE": 4096, "FRAC": 12},
    always=True,
)
print(f"results xml: {blr.test(hdl_toplevel='baseline_restorer', test_module='test_blr', test_dir=here)}")
