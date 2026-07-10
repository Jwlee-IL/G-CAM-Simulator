"""Build + run the cocotb testbench for trapezoidal_shaper.sv with the Icarus runner.
Usage (from rtl/):  python run_cocotb.py
Exits non-zero if any cocotb test fails.
"""
import os
import sys
from cocotb_tools.runner import get_runner

here = os.path.dirname(os.path.abspath(__file__))
os.chdir(here)

runner = get_runner("icarus")
runner.build(
    sources=[os.path.join(here, "trapezoidal_shaper.sv")],
    hdl_toplevel="trapezoidal_shaper",
    build_args=["-g2012"],
    parameters={"M_Q8": 1156, "RISE": 10, "FLAT": 8},   # match trap_ref defaults (tau=5 samples)
    always=True,
)
results = runner.test(
    hdl_toplevel="trapezoidal_shaper",
    test_module="test_trap_shaper",
    test_dir=here,
)
print(f"\nresults xml: {results}")
