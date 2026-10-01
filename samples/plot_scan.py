"""Plots for the configuration scan (Findings theme 3, VV.Gcam.Evidence EV-03).

Input: samples/scan.csv from `montecarlo scan samples/scenario.json samples/scan.csv`.
Output: samples/scan_collapse.png (usable fraction vs D, one panel per cell pitch) and
        samples/scan_pareto.png (usable field vs resolution, every configuration).
"""
import csv, math, pathlib
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = pathlib.Path(__file__).parent
rows = list(csv.DictReader(open(here / "scan.csv", encoding="utf-8")))
for r in rows:
    for k in ("rank", "cell_pitch_mm", "dist_mm", "resolution_mm", "usable_fraction", "usable_fov_area_mm2", "median_error_mm"):
        r[k] = float(r[k])
    r["half"] = math.sqrt(r["usable_fov_area_mm2"]) / 2

ranks = sorted({int(r["rank"]) for r in rows})
pitches = sorted({r["cell_pitch_mm"] for r in rows})
# Rank is ordinal -> one hue, light to dark (sequential blue ramp, steps 250..700).
ramp = ["#86b6ef", "#5598e7", "#2a78d6", "#1c5cab", "#104281", "#0d366b"]
color = {k: ramp[i] for i, k in enumerate(ranks)}
surface, ink, muted, grid = "#fcfcfb", "#1a1a19", "#6b6a64", "#e4e3dd"
plt.rcParams.update({"font.size": 9, "axes.edgecolor": muted, "axes.labelcolor": ink,
                     "xtick.color": muted, "ytick.color": muted, "axes.facecolor": surface,
                     "figure.facecolor": surface, "axes.grid": True, "grid.color": grid, "grid.linewidth": 0.6})

# Collapse: usable fraction vs D per rank, one panel per cell pitch.
fig, axes = plt.subplots(1, len(pitches), figsize=(10, 3.4), sharey=True)
for ax, p in zip(axes, pitches):
    for k in ranks:
        pts = sorted((r["dist_mm"], r["usable_fraction"]) for r in rows if r["rank"] == k and r["cell_pitch_mm"] == p)
        ax.plot([d for d, _ in pts], [u for _, u in pts], color=color[k], lw=2, marker="o", ms=4, label=f"rank {k}")
    ax.axhline(0.9, color=muted, lw=0.8, ls="--")
    ax.set_title(f"cell pitch {p:g} mm", color=ink, fontsize=9)
    ax.set_xlabel("mask–detector distance D (mm)")
    ax.set_ylim(0, 1.05)
axes[0].set_ylabel("usable fraction (error < 1 resolution element)")
axes[0].text(80, 0.92, "90 %", color=muted, fontsize=8, ha="right")
p1 = axes[pitches.index(1.0)]
p1.scatter([20], [0.959], s=60, facecolors="none", edgecolors=ink, zorder=5)
p1.annotate("old record, rank 23\n(0.959, thin-mask era)", (20, 0.959), (34, 0.62), fontsize=8, color=ink,
            arrowprops=dict(arrowstyle="-", color=muted, lw=0.8))
axes[-1].legend(frameon=False, fontsize=8, loc="center right")
fig.suptitle("10 mm tungsten mask, S = 100 mm, 12 × 12 × 1 mm detector: short D collapses high ranks (collimation)",
             color=ink, fontsize=10)
fig.tight_layout()
fig.savefig(here / "scan_collapse.png", dpi=150)

# Pareto: usable half-field vs resolution; filled = >= 90 % usable.
fig, ax = plt.subplots(figsize=(6.4, 4.2))
for k in ranks:
    rs = [r for r in rows if r["rank"] == k]
    ok = [r for r in rs if r["usable_fraction"] >= 0.9]
    no = [r for r in rs if r["usable_fraction"] < 0.9]
    ax.scatter([r["resolution_mm"] for r in ok], [r["half"] for r in ok], s=34, color=color[k], label=f"rank {k}", zorder=3)
    ax.scatter([r["resolution_mm"] for r in no], [r["half"] for r in no], s=34, facecolors="none", edgecolors=color[k], zorder=3)
def mark(r, text, xy):
    ax.annotate(text, (r["resolution_mm"], r["half"]), xy, fontsize=8, color=ink, arrowprops=dict(arrowstyle="-", color=muted, lw=0.8))
best_robust = max((r for r in rows if r["usable_fraction"] >= 0.9), key=lambda r: r["half"])
best_area = max(rows, key=lambda r: r["half"])
mark(best_robust, f"rank {int(best_robust['rank'])} / {best_robust['cell_pitch_mm']:g} mm / D {best_robust['dist_mm']:g}\n±{best_robust['half']:.1f} mm, {best_robust['usable_fraction']:.0%} usable", (best_robust["resolution_mm"] + 2.5, best_robust["half"] - 14))
mark(best_area, f"rank {int(best_area['rank'])} / {best_area['cell_pitch_mm']:g} mm / D {best_area['dist_mm']:g}\n±{best_area['half']:.1f} mm, {best_area['usable_fraction']:.0%} usable", (best_area["resolution_mm"] - 6.5, best_area["half"] - 4))
ax.set_xlabel("resolution element at the source plane (mm)")
ax.set_ylabel("usable half-field ± (mm)")
ax.set_ylim(0, 50)
ax.set_title("Every scanned configuration (filled: ≥ 90 % of the swept field usable)", color=ink, fontsize=9)
ax.legend(frameon=False, fontsize=8, ncol=3, loc="lower right")
fig.tight_layout()
fig.savefig(here / "scan_pareto.png", dpi=150)
print("best robust:", {k: best_robust[k] for k in ("rank", "cell_pitch_mm", "dist_mm", "usable_fraction", "half")})
print("best area:  ", {k: best_area[k] for k in ("rank", "cell_pitch_mm", "dist_mm", "usable_fraction", "half")})
