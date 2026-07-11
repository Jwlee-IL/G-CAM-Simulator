"""Measured Fmax (real place-and-route STA, nextpnr on a Lattice ECP5-6): the pipelined
trapezoidal shaper roughly DOUBLES the clock the direct one can close — the improvement the
open Yosys `ltp` cell-count could not show. Numbers from:
  yosys -p 'read_verilog -sv <top>.sv; synth_ecp5 -top <top> -json x.json'
  nextpnr-ecp5 --json x.json --25k --package CABGA381 --speed 6 --freq 250
Usage:  python rtl/plot_fmax.py
"""
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

names = ["direct\n(un-pipelined)", "pipelined\n(1-adder loops)"]
fmax = [59.2, 118.8]           # MHz, ECP5-6, post-route
cols = ["#c0392b", "#2e8b57"]

fig, ax = plt.subplots(figsize=(6.4, 5.2))
bars = ax.bar(names, fmax, color=cols, width=0.6)
for b, f in zip(bars, fmax):
    ax.text(b.get_x() + b.get_width() / 2, f + 2, f"{f:.0f} MHz", ha="center", fontsize=11, weight="bold")
ax.axhline(100, ls="--", color="#333", lw=1.4)
ax.text(1.45, 102, "100 MSPS ADC", ha="right", fontsize=9, color="#333")
ax.text(0.0, 30, "below\n100 MHz", ha="center", fontsize=9, color="white")
ax.text(1.0, 30, "clears it\nwith margin", ha="center", fontsize=9, color="white")
ax.text(0.5, 88, f"×{fmax[1]/fmax[0]:.1f}", fontsize=17, color="#2c3e50", ha="center", weight="bold")
ax.set_ylabel("achieved Fmax (MHz)"); ax.set_ylim(0, 140)
ax.set_title("Pipelined trapezoidal shaper doubles Fmax\n(real nextpnr place-and-route STA, Lattice ECP5-6)")
ax.grid(axis="y", alpha=0.3)
fig.tight_layout()
fig.savefig("rtl/fmax_ecp5.png", dpi=130)
print("saved rtl/fmax_ecp5.png")
