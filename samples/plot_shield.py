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
ax1.axhline(0.34, ls=":", color="#888"); ax1.text(24, 0.42, "decoder floor 0.34 mm", fontsize=8, color="#888")
ax1.text(0.5, 7.5, "dashed = + calibrated\nbackground subtraction\n(software lever)", fontsize=7.5, color="#555")
ax1.set_xlabel("tungsten wall thickness (mm)"); ax1.set_ylabel("localization RMS (mm)")
ax1.set_yscale("log")
ax1.set_title("(1) RMS vs shield thickness\noptimum depends on background ENERGY")
ax1.legend(fontsize=8); ax1.grid(alpha=0.3, which="both")

# (2) the real trade: RMS vs shield MASS
for bg, (lab, c) in bgs.items():
    m = col(bg, "shield_kg")
    ax2.plot(m, col(bg, "rms_raw_mm"), "-o", color=c, lw=2, ms=4, label=lab)
ax2.axhline(0.34, ls=":", color="#888")
ax2.annotate("scattered: floor at ~0.8 kg (6 mm)", (0.8, 0.34), (1.5, 1.2),
             fontsize=8, color="#2e8b57", arrowprops=dict(arrowstyle="->", color="#2e8b57"))
ax2.annotate("662: needs ~5 kg (20 mm)", (5.1, 0.36), (3.0, 3.0),
             fontsize=8, color="#e67e22", arrowprops=dict(arrowstyle="->", color="#e67e22"))
ax2.text(6.5, 8.5, "Co-60: still failing\nat 10.8 kg (30 mm)\n→ unshieldable,\nuse coded+stripping",
         fontsize=8, color="#c0392b", ha="center")
ax2.set_xlabel("5-sided shield mass (kg)"); ax2.set_ylabel("localization RMS (mm)")
ax2.set_yscale("log")
ax2.set_title("(2) RMS vs shield WEIGHT — the design trade\nspend mass only where it buys SNR")
ax2.legend(fontsize=8); ax2.grid(alpha=0.3, which="both")

fig.suptitle("Optimal 5-sided shield: ~6 mm W (0.8 kg) kills realistic scattered background; a mono-662 field "
             "wants ~15-20 mm; Co-60 can't be shielded in a carriable mass — beat it with coded+spectral, not lead.",
             fontsize=10)
fig.tight_layout(rect=[0, 0, 1, 0.94])
out = os.path.join(here, "shield.png")
fig.savefig(out, dpi=130); print("saved", out)
