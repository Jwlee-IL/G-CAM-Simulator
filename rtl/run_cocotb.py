"""Headless Icarus/cocotb regression matrix, with fresh outputs and no cleanup.
Usage: python rtl/run_cocotb.py [--csharp-vectors DIR] [--crrc-only]
"""
import argparse
import os
from pathlib import Path
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET
from cocotb_tools import config
from cocotb_tools.runner import get_runner
from find_libpython import find_libpython
from crrc_contract import fixtures


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--csharp-vectors", type=Path)
    parser.add_argument("--crrc-only", action="store_true")
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    root = Path(os.environ.get("TEMP", "/tmp")) / ("gcam-rtl-" + uuid.uuid4().hex)
    root.mkdir()
    tally = dict(tests=0, passed=0, failed=0, skipped=0)

    def run(top, source, module, params, name, extra=None):
        directory = root / name
        directory.mkdir()
        runner = get_runner("icarus")
        runner.build(sources=[here / source], hdl_toplevel=top, parameters=params,
                     build_dir=directory, always=True, clean=False)
        results = directory / "results.xml"
        env = os.environ.copy()
        env.update({"LIBPYTHON_LOC": str(Path(sys.prefix) / f"python{sys.version_info.major}{sys.version_info.minor}.dll") if os.name == "nt" else find_libpython(),
                    "PYGPI_PYTHON_BIN": sys.executable, "PYTHONPATH": str(here),
                    "COCOTB_TOPLEVEL": top, "TOPLEVEL_LANG": "verilog",
                    "COCOTB_TEST_MODULES": module, "COCOTB_RESULTS_FILE": str(results),
                    "COCOTB_RANDOM_SEED": "20261002"})
        env.update(extra or {})
        # Direct cocotb invocation avoids Runner.test's unconditional results-file unlink.
        subprocess.run(["vvp", "-M", str(config.libs_dir), "-m", config.lib_name("vpi", "icarus"),
                        str(directory / "sim.vvp"), "-none"], cwd=here, env=env, check=True)
        cases = ET.parse(results).findall(".//testcase")
        if not cases:
            raise RuntimeError(f"{name}: no tests")
        failed = sum(c.find("failure") is not None or c.find("error") is not None for c in cases)
        skipped = sum(c.find("skipped") is not None for c in cases)
        passed = len(cases) - failed - skipped
        tally["tests"] += len(cases); tally["passed"] += passed
        tally["failed"] += failed; tally["skipped"] += skipped
        print(f"{name}: {passed} passed, {failed} failed, {skipped} skipped; {results}", flush=True)
        if failed:
            raise RuntimeError(f"{name}: {failed} test failures")

    if not args.crrc_only:
        for top in ("trapezoidal_shaper", "trapezoidal_shaper_pl"):
            run(top, top + ".sv", "test_trap_shaper", {"M_Q8": 1156, "RISE": 10, "FLAT": 8}, top)
        run("baseline_restorer", "baseline_restorer.sv", "test_blr",
            {"WACC": 32, "GATE": 4096, "FRAC": 12}, "baseline_restorer")
    for name, spec in fixtures().items():
        for fractional in (0, 12):
            extra = {"GCAM_CRRC_FIXTURE": name, "GCAM_CRRC_F": str(fractional)}
            if args.csharp_vectors:
                extra["GCAM_CRRC_VECTORS"] = str(args.csharp_vectors.resolve())
            run("crrc_shaper", "crrc_shaper.sv", "test_crrc",
                {"WIN": 16, "ORDER": spec["order"], "A_Q16": spec["a"], "K_Q16": spec["k"],
                 "Q": 16, "F": fractional, "WACC": 48}, f"{name}-f{fractional}", extra)
    print(f"cocotb total: {tally}; artifacts: {root}", flush=True)


if __name__ == "__main__":
    main()
