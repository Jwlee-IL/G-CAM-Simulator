"""TODO-19 RD-9 / RD-10: regenerate the committed readout schematics, netlists and netlist checks (stdlib only).

    dotnet build Gcam.sln -c Release
    python samples/readout/regenerate.py            # writes docs/assets/readout/*

For each entry below: `montecarlo readout-export` writes <prefix>.config.json (the engine-resolved configuration and the
engine's DC charge fractions) and, for a solved circuit, <prefix>.cir; netlist_check.py solves the netlist independently
and writes <prefix>.comparison.json (fails when the deviation exceeds the derived bound); schematic.py draws <prefix>.svg.
Configurations come from samples/evidence/readout/request-v1.json and samples/scenario.json — the study's own inputs.
"""
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
CLI = REPO / 'src/Gcam.Cli/bin/Release/net9.0/Gcam.Cli.dll'
OUT = REPO / 'docs/assets/readout'
REQUEST = 'samples/evidence/readout/request-v1.json'
# (file prefix, geometry, readout, trigger, draw the schematic)
ENTRIES = [
    ('readout-p3.2-g0.2-dpc-r0.1', 'p3.2-g0.2', 'Anger-DPC-r0.1', 'Sum50', True),
    ('readout-p3.2-g0.2-dpc-r1.0', 'p3.2-g0.2', 'Anger-DPC-r1.0', 'Sum50', False),
    ('readout-p3.2-g0.2-grid', 'p3.2-g0.2', 'Anger-Grid', 'Sum50', True),
]


def run(args):
    result = subprocess.run(args, cwd=REPO, capture_output=True, text=True)
    sys.stdout.write(result.stdout)
    if result.returncode:
        sys.stderr.write(result.stderr)
        raise SystemExit(f'failed: {" ".join(map(str, args))}')


def main():
    if not CLI.exists():
        raise SystemExit(f'{CLI} not found - build first: dotnet build Gcam.sln -c Release')
    OUT.mkdir(parents=True, exist_ok=True)
    for prefix, geometry, readout, trigger, draw in ENTRIES:
        target = (OUT / prefix).relative_to(REPO).as_posix()
        run(['dotnet', str(CLI), 'readout-export', 'samples/scenario.json', REQUEST, geometry, readout, trigger, target])
        run([sys.executable, '-B', str(HERE / 'netlist_check.py'), target, '--write'])
        if draw:
            run([sys.executable, '-B', str(HERE / 'schematic.py'), target])
    return 0


if __name__ == '__main__':
    sys.exit(main())
