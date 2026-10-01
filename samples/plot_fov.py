"""Field of view at field distance + out-of-field cue (run `montecarlo fov samples/scenario_handheld.json`).
(1) how far off axis a single acquisition still localizes the source, non-cyclic vs cyclic decoding;
(2) whether the left/right side of an out-of-field source can be read from the flood centroid or the decoded peak;
(3) the "outside the field" flag; (4) wrong in-field answers and how many of them the centroid flag catches.
Usage:  python samples/plot_fov.py
"""
import os, csv
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(os.path.join(here, "fov.csv"))))
FC_HALF = 3.64  # fully coded half-angle at D = 55 mm, rank 7, 1 mm cells


def series(dist, direction, n0, bsr, key):
    sel = [r for r in rows if float(r["distance_mm"]) == dist and float(r["direction_deg"]) == direction
           and float(r["onaxis_counts"]) == n0 and float(r["bsr"]) == bsr]
    sel.sort(key=lambda r: float(r["angle_deg"]))
    return np.array([float(r["angle_deg"]) for r in sel]), np.array([float(r[key]) for r in sel])


fig, axes = plt.subplots(1, 4, figsize=(22, 5.4))
cases = [(5000, 0.0, "#1f77b4", "N0 5000, no background"), (500, 0.0, "#2ca02c", "N0 500, no background"),
         (500, 1.0, "#d62728", "N0 500, background = N0")]

ax = axes[0]
for n0, bsr, c, lab in cases:
    a, nc = series(1000, 0, n0, bsr, "loc_noncyclic")
    _, cy = series(1000, 0, n0, bsr, "loc_cyclic")
    ax.plot(a, nc, "-o", color=c, ms=3, lw=1.8, label=f"non-cyclic, {lab}")
    ax.plot(a, cy, ":", color=c, lw=1.4, label=f"cyclic, {lab}")
a, eff = series(1000, 0, 5000, 0.0, "rel_efficiency")
ax.plot(a, eff, "-", color="#777", lw=1.0, label="relative efficiency")
ax.axvspan(0, FC_HALF, color="#eee", zorder=0)
ax.text(0.2, 0.05, "fully coded", fontsize=8, color="#666")
ax.set_xlabel("off-axis angle (°)"); ax.set_ylabel("fraction localized within 1.04°")
ax.set_title("(1) usable field, S = 1 m, along x")
ax.legend(fontsize=7); ax.grid(alpha=0.3)

ax = axes[1]
for n0, bsr, c, lab in cases:
    a, sc = series(1000, 0, n0, bsr, "side_centroid")
    _, sp = series(1000, 0, n0, bsr, "side_peak")
    ax.plot(a[1:], sc[1:], "-o", color=c, ms=3, lw=1.8, label=f"centroid, {lab}")
    ax.plot(a[1:], sp[1:], ":", color=c, lw=1.4, label=f"decoded peak, {lab}")
ax.axvspan(0, FC_HALF, color="#eee", zorder=0)
ax.axhline(0.5, color="#999", lw=0.8)
ax.set_xlabel("off-axis angle (°)"); ax.set_ylabel("fraction with the correct side")
ax.set_title("(2) left / right cue, S = 1 m")
ax.legend(fontsize=7); ax.grid(alpha=0.3)

ax = axes[2]
for n0, bsr, c, lab in cases:
    a, oc = series(1000, 0, n0, bsr, "outside_centroid")
    _, op = series(1000, 0, n0, bsr, "outside_peak")
    ax.plot(a, oc, "-o", color=c, ms=3, lw=1.8, label=f"centroid, {lab}")
    ax.plot(a, op, ":", color=c, lw=1.4, label=f"decoded peak, {lab}")
ax.axvspan(0, FC_HALF, color="#eee", zorder=0)
ax.set_xlabel("off-axis angle (°)"); ax.set_ylabel("fraction flagged 'outside the field'")
ax.set_title("(3) out-of-field flag, S = 1 m")
ax.legend(fontsize=7); ax.grid(alpha=0.3)

ax = axes[3]
for n0, bsr, c, lab in cases:
    a, fi = series(1000, 0, n0, bsr, "false_infield")
    _, fu = series(1000, 0, n0, bsr, "false_infield_unflagged")
    ax.plot(a, fi, ":", color=c, lw=1.4, label=f"wrong in-field spot, {lab}")
    ax.plot(a, fu, "-o", color=c, ms=3, lw=1.8, label=f"... not flagged by centroid, {lab}")
ax.axvspan(0, FC_HALF, color="#eee", zorder=0)
ax.set_xlabel("off-axis angle (°)"); ax.set_ylabel("fraction of acquisitions")
ax.set_title("(4) wrong in-field answers, S = 1 m")
ax.legend(fontsize=7); ax.grid(alpha=0.3)

fig.tight_layout()
out = os.path.join(here, "fov.png")
fig.savefig(out, dpi=110)
print("wrote", out)
