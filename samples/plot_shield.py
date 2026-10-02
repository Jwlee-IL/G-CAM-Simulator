"""Optimal 5-sided tungsten shield thickness (run `montecarlo shield samples/scenario.json`).
The useful thickness is set by the BACKGROUND ENERGY: low-E scattered noise dies in a few mm,
a 662 keV background needs ~20 mm, and Co-60 is unshieldable in a carriable mass.
The curves are one run (samples/shield.csv, seed 12345); the picked thicknesses and masses are annotated from the
128-seed ensemble in samples/evidence/results/aggregate_discrete.csv (TODO-27), not from this one run.
Usage:  python samples/plot_shield.py
"""
import os, csv
from collections import defaultdict
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(os.path.join(here, "shield.csv"))))

# Pick frequencies over the seed ensemble: {metric: [(value, count, N), ...]} sorted by count, most frequent first.
picks = defaultdict(list)
for r in csv.DictReader(open(os.path.join(here, "evidence", "results", "aggregate_discrete.csv"))):
    if r["key"].startswith("shield/"):
        picks[r["key"]].append((float(r["value"]), int(r["count"]), int(r["N"])))
for k in picks:
    picks[k].sort(key=lambda t: -t[1])

def pick_text(name):
    """'8 mm, 0.98 kg (109/128)' for each pick of the knee rule, most frequent first."""
    masses = dict((c, m) for m, c, _ in picks[f"shield/picked_mass/{name}"])
    return " or ".join(f"{t:.0f} mm, {masses.get(c, float('nan')):.2f} kg ({c}/{n})"
                       for t, c, n in picks[f"shield/knee/{name}"])

unchanged = next(c for v, c, n in picks["shield/directional_delta"] if v == 0)
n_dir = picks["shield/directional_delta"][0][2]
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
# decode rejects a flat pedestal to DC but not this structure; over the ensemble the pick rarely moves.
td = col("scattered_250keV_dir", "thickness_mm")
if len(td):
    ax1.plot(td, col("scattered_250keV_dir", "rms_raw_mm"), "-.s", color="#1f6f45", lw=1.6, ms=4,
             label="scattered, DIRECTIONAL leak (80% side-wall)")
floor = min(float(r["rms_raw_mm"]) for r in rows)   # this run's best RMS: the decoder / counting floor
ax1.axhline(floor, ls=":", color="#888"); ax1.text(0.3, floor * 1.06, f"floor {floor:.2f} mm (this run)", fontsize=8, color="#888")
ax1.text(16, 2.2, "dash-dot = same total leak,\nbut edge-weighted (side walls)\n"
         f"→ same pick in {unchanged}/{n_dir} seeds", fontsize=7, color="#1f6f45")
ax1.set_xlabel("tungsten wall thickness (mm)"); ax1.set_ylabel("localization RMS (mm)")
ax1.set_yscale("log")
ax1.set_title("(1) RMS vs shield thickness\noptimum depends on background ENERGY")
ax1.legend(fontsize=8); ax1.grid(alpha=0.3, which="both")

# (2) the real trade: RMS vs shield MASS
for bg, (lab, c) in bgs.items():
    m = col(bg, "shield_kg")
    ax2.plot(m, col(bg, "rms_raw_mm"), "-o", color=c, lw=2, ms=4, label=lab)
ax2.axhline(floor, ls=":", color="#888")

def at(bg, thickness):
    """(mass, RMS) of this run's row — where an annotation arrow points."""
    r = next(r for r in rows if r["bg"] == bg and float(r["thickness_mm"]) == thickness)
    return float(r["shield_kg"]), float(r["rms_raw_mm"])

ax2.annotate("scattered: " + pick_text("scattered"), at("scattered_250keV", picks["shield/knee/scattered"][0][0]),
             (1.4, 1.5), fontsize=8, color="#2e8b57", arrowprops=dict(arrowstyle="->", color="#2e8b57"))
ax2.annotate("662: " + pick_text("cs").replace(" or ", "\nor "), at("Cs137_662keV", picks["shield/knee/cs"][0][0]), (5.0, 0.95),
             fontsize=8, color="#e67e22", arrowprops=dict(arrowstyle="->", color="#e67e22"))
ax2.text(7.6, 2.4, "Co-60:\n" + pick_text("co").replace(" or ", "\nor ")
         + "\n→ not carriable (≤ 2.5 kg budget),\nuse coded+stripping", fontsize=8, color="#c0392b", ha="center")
ax2.set_xlabel("5-sided shield mass (kg)"); ax2.set_ylabel("localization RMS (mm)")
ax2.set_yscale("log")
ax2.set_title("(2) RMS vs shield WEIGHT — the design trade\nspend mass only where it buys SNR")
ax2.legend(fontsize=8); ax2.grid(alpha=0.3, which="both")

fig.suptitle("Optimal 5-sided shield (NIST-mu), knee picks over 128 seeds: scattered " + pick_text("scattered")
             + "; 662 " + pick_text("cs") + ";\nCo-60 " + pick_text("co")
             + " — not carriable → coded+spectral, not lead. Curves: one run (seed 12345).", fontsize=9)
fig.tight_layout(rect=[0, 0, 1, 0.94])
out = os.path.join(here, "shield.png")
fig.savefig(out, dpi=130); print("saved", out)
