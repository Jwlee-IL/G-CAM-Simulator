"""Optimal 5-sided tungsten shield thickness (run `montecarlo shield samples/scenario_handheld.json`).
The useful thickness is set by the BACKGROUND ENERGY: low-E scattered noise dies in a few mm,
a 662 keV background needs ~15-20 mm, and Co-60 is unshieldable in a carriable mass.
Usage:  python samples/plot_shield.py
"""
import os, csv
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(os.path.join(here, "shield.csv"))))
bgs = {
    "scattered_250keV": ("scattered ~250 keV (room/degraded)", "#2e8b57"),
    "Cs137_662keV":     ("mono 662 keV background", "#e67e22"),
    "Co60_1250keV":     ("Co-60 ~1250 keV", "#c0392b"),
}
def col(bg, key):
    return np.array([float(r[key]) for r in rows if r["bg"] == bg])

fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(14, 5.7))

# (1) RMS vs thickness (raw solid, calibrated-subtraction dashed)
for bg, (lab, c) in bgs.items():
    t = col(bg, "thickness_mm")
    ax1.plot(t, col(bg, "rms_raw_mm"), "-o", color=c, lw=2, ms=4, label=lab)
    ax1.plot(t, col(bg, "rms_calib_mm"), "--", color=c, lw=1.2, alpha=0.7)
# directional (edge-weighted side-wall) leak of the SAME total, for the scattered background: the coded
# decode rejects a flat pedestal to DC but not this structure, so the knee shifts thicker (6 -> 8 mm).
td = col("scattered_250keV_dir", "thickness_mm")
if len(td):
    ax1.plot(td, col("scattered_250keV_dir", "rms_raw_mm"), "-.s", color="#1f6f45", lw=1.6, ms=4,
             label="scattered, DIRECTIONAL leak (80% side-wall)")
ax1.axhline(0.34, ls=":", color="#888"); ax1.text(24, 0.42, "decoder floor 0.34 mm", fontsize=8, color="#888")
ax1.text(0.5, 6.5, "dash-dot = same total leak,\nbut edge-weighted (side walls)\n→ needs a bit more shield", fontsize=7, color="#1f6f45")
ax1.set_xlabel("tungsten wall thickness (mm)"); ax1.set_ylabel("localization RMS (mm)")
ax1.set_yscale("log")
ax1.set_title("(1) RMS vs shield thickness\noptimum depends on background ENERGY")
ax1.legend(fontsize=8); ax1.grid(alpha=0.3, which="both")

# (2) the real trade: RMS vs shield MASS
for bg, (lab, c) in bgs.items():
    m = col(bg, "shield_kg")
    ax2.plot(m, col(bg, "rms_raw_mm"), "-o", color=c, lw=2, ms=4, label=lab)
ax2.axhline(0.34, ls=":", color="#888")
ax2.annotate("scattered: floor at ~0.65 kg (6 mm)", (0.65, 0.60), (1.6, 1.5),
             fontsize=8, color="#2e8b57", arrowprops=dict(arrowstyle="->", color="#2e8b57"))
ax2.annotate("662: needs ~4.5 kg (20 mm)", (4.49, 0.36), (2.8, 3.0),
             fontsize=8, color="#e67e22", arrowprops=dict(arrowstyle="->", color="#e67e22"))
ax2.text(8.2, 6.0, "Co-60: only at\n~9.8 kg (30 mm)\n→ not carriable,\nuse coded+stripping",
         fontsize=8, color="#c0392b", ha="center")
ax2.set_xlabel("5-sided shield mass (kg)"); ax2.set_ylabel("localization RMS (mm)")
ax2.set_yscale("log")
ax2.set_title("(2) RMS vs shield WEIGHT — the design trade\nspend mass only where it buys SNR")
ax2.legend(fontsize=8); ax2.grid(alpha=0.3, which="both")

fig.suptitle("Optimal 5-sided shield (NIST-mu): ~6 mm W (0.65 kg) kills scattered background — ~8 mm if the leak is directional; "
             "662 wants ~20 mm; Co-60 only at ~30 mm/9.8 kg (not carriable → coded+spectral, not lead).",
             fontsize=9)
fig.tight_layout(rect=[0, 0, 1, 0.94])
out = os.path.join(here, "shield.png")
fig.savefig(out, dpi=130); print("saved", out)
