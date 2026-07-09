"""Plot the coded-aperture depth (z) estimation (run `montecarlo depth samples/scenario.json`):
focus (refocus sharpness) vs assumed source distance for several true distances, and the
estimated-vs-true accuracy with the depth resolution (focus-curve width).

Usage:  python samples/plot_depth.py
"""
import os
import csv
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(os.path.join(here, "depth.csv"))))

trues = sorted({float(r["true_s_mm"]) for r in rows})
cmap = plt.get_cmap("viridis")
colors = {t: cmap(i / max(1, len(trues) - 1)) for i, t in enumerate(trues)}

fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(14, 5.4))

est_of, res_of = {}, {}
for t in trues:
    sub = [r for r in rows if float(r["true_s_mm"]) == t]
    a = np.array([float(r["assumed_s_mm"]) for r in sub])
    f = np.array([float(r["focus"]) for r in sub])
    est = float(sub[0]["estimated_s_mm"])
    est_of[t] = est
    fn = (f - f.min()) / (f.max() - f.min() + 1e-9)          # normalize for overlay
    ax1.plot(a, fn, "-", color=colors[t], lw=1.8, label=f"S={t:.0f}mm (est {est:.0f})")
    ax1.axvline(t, color=colors[t], ls=":", lw=1.0)
    # depth resolution ~ full width where focus > half its range (the plateau width)
    above = a[fn > 0.5]
    res_of[t] = (above.max() - above.min()) if len(above) > 1 else 0.0

ax1.set_xlabel("assumed source distance S (mm)")
ax1.set_ylabel("refocus sharpness (normalized)")
ax1.set_title("Depth from refocusing: focus peaks at the true S,\nand broadens with distance (resolution degrades)")
ax1.legend(fontsize=8); ax1.grid(alpha=0.3)

lo, hi = min(trues) - 20, max(trues) + 40
ax2.plot([lo, hi], [lo, hi], "--", color="gray", lw=1, label="ideal (est = true)")
for t in trues:
    ax2.errorbar(t, est_of[t], yerr=res_of[t] / 2.0, fmt="o", color=colors[t], ms=8,
                 capsize=4, elinewidth=1.5)
    ax2.annotate(f"{est_of[t]-t:+.0f}mm", (t, est_of[t]), fontsize=8,
                 textcoords="offset points", xytext=(8, -3))
ax2.set_xlabel("true source distance S (mm)")
ax2.set_ylabel("estimated S (mm)  [error bar = depth resolution]")
ax2.set_title("Estimate tracks truth; error bar (focus-curve width) grows\n"
              "with distance — the intrinsic near>far coded-aperture depth limit")
ax2.legend(fontsize=8); ax2.grid(alpha=0.3); ax2.set_xlim(lo, hi); ax2.set_ylim(lo, hi)

fig.tight_layout()
fig.savefig(os.path.join(here, "depth_estimation.png"), dpi=130)
print("saved samples/depth_estimation.png")
for t in trues:
    print(f"  S={t:6.0f}mm  est={est_of[t]:6.1f}mm  err={est_of[t]-t:+5.1f}mm  resolution~{res_of[t]:.0f}mm")
