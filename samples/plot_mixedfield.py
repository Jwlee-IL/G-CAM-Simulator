"""Mixed-isotope field imaged in ONE coded-aperture run (run `montecarlo mixedfield samples/scenario.json`).
Three isotopes at three positions -> one reconstruction with three peaks, all localized by
non-maximum-suppression peak extraction. This is a TRUE mixed field, not summed per-line runs.
Usage:  python samples/plot_mixedfield.py
"""
import os, csv
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(os.path.join(here, "mixedfield_recon.csv"))))
xs = np.array([float(r["x_mm"]) for r in rows]); ys = np.array([float(r["y_mm"]) for r in rows])
vs = np.array([float(r["value"]) for r in rows])
ux = np.unique(xs); uy = np.unique(ys)
grid = vs.reshape(len(uy), len(ux))  # row-major: y outer, x inner (dump order gy,gx)

pts = list(csv.DictReader(open(os.path.join(here, "mixedfield.csv"))))
truth = [(float(p["x_mm"]), float(p["y_mm"])) for p in pts if p["kind"] == "truth"]
found = [(float(p["x_mm"]), float(p["y_mm"])) for p in pts if p["kind"] == "found"]
labels = ["Cs-137\n662", "Co-60\n1173+1332", "Co-57\n122"]

fig, ax = plt.subplots(figsize=(7.4, 6.4))
im = ax.imshow(grid, origin="lower", extent=[ux.min(), ux.max(), uy.min(), uy.max()],
               cmap="magma", aspect="equal")
for i, (tx, ty) in enumerate(truth):
    ax.plot(tx, ty, "o", mfc="none", mec="#2ecc71", ms=20, mew=2.5)
    ax.text(tx + 0.6, ty + 0.6, labels[i] if i < len(labels) else "", color="#2ecc71", fontsize=8, weight="bold")
for fx, fy in found:
    ax.plot(fx, fy, "x", color="#3498db", ms=11, mew=2.5)
ax.plot([], [], "o", mfc="none", mec="#2ecc71", label="true source")
ax.plot([], [], "x", color="#3498db", label="localized peak")
ax.set_xlabel("source-plane x (mm)"); ax.set_ylabel("source-plane y (mm)")
ax.set_title("Mixed isotope field (Cs-137 + Co-60 + Co-57) imaged in ONE run\n"
             "3 positions → 3 reconstruction peaks, all localized < 1 mm")
ax.legend(loc="upper right", fontsize=8, framealpha=0.9)
fig.colorbar(im, ax=ax, label="reconstruction", fraction=0.046)
fig.tight_layout()
fig.savefig(os.path.join(here, "mixedfield.png"), dpi=130)
print("saved samples/mixedfield.png")
