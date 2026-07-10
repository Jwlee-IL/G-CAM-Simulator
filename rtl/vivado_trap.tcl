# Exact Artix-7 timing for trapezoidal_shaper.sv, in Vivado ML Standard (FREE for Artix-7).
#
# Run (no GUI, no paid license). Compare the direct vs pipelined shaper to see the Fmax gain
# the open Yosys flow cannot show (the constant multiply hides it in cell-count):
#     vivado -mode batch -source vivado_trap.tcl                                   ;# direct, xc7a35t, 200 MHz target
#     vivado -mode batch -source vivado_trap.tcl -tclargs trapezoidal_shaper_pl    ;# pipelined
#     vivado -mode batch -source vivado_trap.tcl -tclargs trapezoidal_shaper_pl xc7a100tcsg324-1 3.0
#            (optional: <top> <part> <target_period_ns>)
#
# It synthesizes + places + routes, constrains clk at the target period, and reports the
# achieved Fmax = 1000 / (period - WNS). WNS>0 = met with margin; <0 = the real path is
# slower than the target (Fmax is still computed correctly from the slack).

set top    [expr {$argc >= 1 ? [lindex $argv 0] : "trapezoidal_shaper"}]
set part   [expr {$argc >= 2 ? [lindex $argv 1] : "xc7a35tcsg324-1"}]   ;# -1 = slowest speed grade (conservative)
set period [expr {$argc >= 3 ? [lindex $argv 2] : 5.0}]                  ;# ns (5.0 = 200 MHz target)
set rtl    [file join [file dirname [info script]] ${top}.sv]

puts "=== $top on $part, target ${period} ns ==="
read_verilog -sv $rtl
synth_design -top $top -part $part

# Constrain the only clock and run implementation.
create_clock -name clk -period $period [get_ports clk]
opt_design
place_design
route_design

# Post-route timing.
set wns [get_property SLACK [get_timing_paths -max_paths 1 -nworst 1 -setup]]
set fmax [expr {1000.0 / ($period - $wns)}]
puts "----------------------------------------------------------------"
puts [format "PART        : %s" $part]
puts [format "TARGET      : %.3f ns (%.1f MHz)" $period [expr {1000.0/$period}]]
puts [format "WNS (setup) : %.3f ns" $wns]
puts [format "ACHIEVED    : %.3f ns  ->  Fmax = %.1f MHz" [expr {$period - $wns}] $fmax]
puts "----------------------------------------------------------------"
report_utilization -hierarchical
report_timing_summary -delay_type max -max_paths 3 -file trap_timing.rpt
puts "utilization above; full timing in trap_timing.rpt"
