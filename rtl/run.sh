#!/usr/bin/env bash
# End-to-end RTL peak-detector co-simulation:
#   Python pulse shaping -> adc.txt -> Icarus (iverilog+vvp) -> peaks.txt -> Python scoring/plot
#
# Usage: ./run.sh [rate_per_s] [n_events] [label]
set -e
cd "$(dirname "$0")"
export PATH="$HOME/scoop/shims:$PATH"

RATE="${1:-50000}"
NEVENTS="${2:-2000}"
LABEL="${3:-run}"

echo "1/4 stimulus (rate=$RATE, events=$NEVENTS)"
python gen_stimulus.py "$RATE" "$NEVENTS" .

echo "2/4 compile"
iverilog -g2012 -o sim.vvp tb_peak_detector.sv peak_detector.sv

echo "3/4 simulate"
vvp sim.vvp

echo "4/4 analyze"
python analyze.py . "$LABEL"
