"""Tapered (hourglass) channels = a real wide-FOV fix for THICK coded-aperture masks.
Run `montecarlo masktaper samples/scenario.json` first. A thick STRAIGHT mask collimates
off-axis rays (edge/center efficiency < 1); bevelling the walls (code stays at the mid-plane)
recovers the edge while keeping the code sharp. Distinct from focusing (theme 20), which
NARROWS the FOV to a focal point.
Usage:  python samples/plot_masktaper.py
"""
import os, csv
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Polygon

here = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(os.path.join(here, "masktaper.csv"))))
tap = np.array([float(r["taper_deg"]) for r in rows])
effC = np.array([float(r["eff_center"]) for r in rows])
effE = np.array([float(r["eff_edge"]) for r in rows])
ratio = np.array([float(r["edge_ratio"]) for r in rows])
rms = np.array([float(r["rms_edge_mm"]) for r in rows])

fig, (axs, axr) = plt.subplots(1, 2, figsize=(13.5, 5.4), gridspec_kw={"width_ratios": [1, 1.3]})

# --- (left) schematic: straight tube vs hourglass channel through a thick slab ---
def channel(ax, x0, bevel, title):
    t, half = 4.0, 1.0          # slab thickness (drawing units) and half-cell
    # tungsten slab block
    ax.add_patch(plt.Rectangle((x0 - 2, -t/2), 4, t, fc="#8d99a0", ec="k"))
    # open channel outline (white)
    if bevel == 0:
        pts = [(x0-half, -t/2), (x0+half, -t/2), (x0+half, t/2), (x0-half, t/2)]
    else:
        pts = [(x0-half-bevel, -t/2), (x0+half+bevel, -t/2),
               (x0+half, 0), (x0+half+bevel, t/2), (x0-half-bevel, t/2), (x0-half, 0)]
    ax.add_patch(Polygon(pts, closed=True, fc="white", ec="k"))
    # an off-axis ray
    ray_x = np.array([x0 - 1.9, x0 + 1.9]); ray_y = np.array([-t/2 - 0.6, t/2 + 0.6])
    clipped = bevel == 0
    ax.plot(ray_x, ray_y, color="#c0392b" if clipped else "#2e8b57", lw=2)
    ax.text(x0, t/2 + 1.0, title, ha="center", fontsize=9)
    ax.text(x0, -t/2 - 1.2, "off-axis ray\nCLIPPED" if clipped else "off-axis ray\nPASSES",
            ha="center", fontsize=7.5, color="#c0392b" if clipped else "#2e8b57")

channel(axs, 0, 0.0, "straight (thick)")
channel(axs, 6, 0.9, "hourglass (bevelled)")
axs.text(3, -4.4, "code defined at the MID-PLANE (dashed); walls flare toward both faces",
         ha="center", fontsize=7.5, color="#555")
axs.plot([-2.5, 8.5], [0, 0], ls="--", color="#555", lw=0.8)
axs.set_xlim(-3, 9); axs.set_ylim(-5, 4); axs.axis("off"); axs.set_aspect("equal")
axs.set_title("(1) Straight collimates off-axis; hourglass passes it", fontsize=9.5)

# --- (right) recovery curves ---
axr.plot(tap, ratio, "o-", color="#2e8b57", lw=2, label="edge/center efficiency")
axr.axhline(1.0, ls=":", color="#888")
axr.annotate("straight: 0.82\n(thick mask collimates)", (0, 0.82), (3, 0.7),
             fontsize=8, color="#c0392b", arrowprops=dict(arrowstyle="->", color="#c0392b"))
axr.annotate("~4° bevel → 0.99\n(uniform FOV)", (4, 0.99), (8, 0.86),
             fontsize=8, color="#2e8b57", arrowprops=dict(arrowstyle="->", color="#2e8b57"))
axr.set_xlabel("channel wall taper (deg)"); axr.set_ylabel("edge/center efficiency", color="#2e8b57")
axr.set_ylim(0.6, 1.05); axr.grid(alpha=0.3)
ax2 = axr.twinx()
ax2.plot(tap, rms, "s--", color="#3b6ea5", lw=1.4, label="RMS @ edge")
ax2.set_ylabel("localization RMS @ edge (mm)", color="#3b6ea5"); ax2.set_ylim(0, 1.0)
ax2.text(10, 0.50, "RMS flat ~0.43 mm\n(code stays sharp)", fontsize=8, color="#3b6ea5")
axr.set_title("(2) A ~4° bevel recovers the FOV uniformity of a 25 mm mask\n(unlike focusing, which narrows the FOV)", fontsize=9.5)

fig.suptitle("Tapered (hourglass) channels: the real wide-FOV fix for a THICK mask — bevelled walls remove the "
             "off-axis collimation (edge/center 0.82→0.99 at ~4°) while the mid-plane keeps the code sharp.", fontsize=9.5)
fig.tight_layout(rect=[0, 0, 1, 0.94])
out = os.path.join(here, "masktaper.png")
fig.savefig(out, dpi=130); print("saved", out)
