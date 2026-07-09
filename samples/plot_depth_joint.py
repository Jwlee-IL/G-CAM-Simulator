"""Plot depth estimation under Poisson noise + joint lateral+depth (run
`montecarlo depth-joint samples/scenario.json`): depth RMS vs counts (near/far, direct vs
joint) and the lateral RMS of the joint estimate.

Usage:  python samples/plot_depth_joint.py
"""
import os
import csv
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(os.path.join(here, "depth_joint.csv"))))
scenarios = []
for r in rows:
    if r["scenario"] not in scenarios:
        scenarios.append(r["scenario"])

style = {
    "onaxis_near":  ("#2ecc71", "o-", "on-axis near S=60 (direct)"),
    "onaxis_far":   ("#c0392b", "s-", "on-axis far S=150 (direct)"),
    "offaxis_near": ("#27ae60", "o--", "off-axis near S=60 (joint x,y,z)"),
    "offaxis_far":  ("#e67e22", "s--", "off-axis far S=150 (joint x,y,z)"),
}

fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(14, 5.4))

for sc in scenarios:
    sub = [r for r in rows if r["scenario"] == sc]
    c = np.array([float(r["counts"]) for r in sub])
    drms = np.array([float(r["depth_rms_mm"]) for r in sub])
    col, mk, lab = style[sc]
    ax1.loglog(c, drms, mk, color=col, lw=2, label=lab)
ax1.set_xlabel("detected counts"); ax1.set_ylabel("depth RMS error (mm)")
ax1.set_title("Depth error vs counts: near field converges to sub-mm,\n"
              "far field floors ~10 mm (broad focus); joint adds coupling noise")
ax1.legend(fontsize=8); ax1.grid(alpha=0.3, which="both")

for sc in ("offaxis_near", "offaxis_far"):
    sub = [r for r in rows if r["scenario"] == sc]
    c = np.array([float(r["counts"]) for r in sub])
    lrms = np.array([float(r["lateral_rms_mm"]) for r in sub])
    col, mk, lab = style[sc]
    ax2.loglog(c, lrms, mk, color=col, lw=2, label=lab.replace(" (joint x,y,z)", ""))
ax2.set_xlabel("detected counts"); ax2.set_ylabel("lateral (x,y) RMS error (mm)")
ax2.set_title("Joint estimate: lateral (x,y) stays ~mm even while\n"
              "the depth (z) is uncertain — x,y and z are not equally constrained")
ax2.legend(fontsize=8); ax2.grid(alpha=0.3, which="both")

fig.tight_layout()
fig.savefig(os.path.join(here, "depth_joint.png"), dpi=130)
print("saved samples/depth_joint.png")
