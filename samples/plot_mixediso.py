"""Isotope separation from a TRUE mixed field, one run (run `montecarlo mixediso samples/scenario.json`).
Cs-137 (662) + a strong Co-60 (1173+1332) imaged through a 662 keV ±10% window + crystal Compton.
Energy windowing alone can't reject the Co downscatter (it lands in the 662 window), but the coded
decode images Cs at its position and the Co contamination at Co's — spatial separation (theme 15,
now from a real mixed source, not summed per-line runs).
Usage:  python samples/plot_mixediso.py
"""
import os, csv
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(os.path.join(here, "mixediso_recon.csv"))))
xs = np.array([float(r["x_mm"]) for r in rows]); ys = np.array([float(r["y_mm"]) for r in rows])
vs = np.array([float(r["value"]) for r in rows])
ux = np.unique(xs); uy = np.unique(ys)
grid = vs.reshape(len(uy), len(ux))

pts = list(csv.DictReader(open(os.path.join(here, "mixediso.csv"))))
truth = [(float(p["x_mm"]), float(p["y_mm"])) for p in pts if p["kind"] == "truth"]
labels = ["Cs-137 (real 662)", "Co-60 downscatter\n(contamination)"]

fig, ax = plt.subplots(figsize=(7.6, 6.4))
im = ax.imshow(grid, origin="lower", extent=[ux.min(), ux.max(), uy.min(), uy.max()],
               cmap="magma", aspect="equal")
cols = ["#2ecc71", "#e74c3c"]
for i, (tx, ty) in enumerate(truth):
    ax.plot(tx, ty, "o", mfc="none", mec=cols[i], ms=22, mew=2.5)
    ax.text(tx + 0.6, ty + 0.8, labels[i] if i < len(labels) else "", color=cols[i], fontsize=9, weight="bold")
ax.set_xlabel("source-plane x (mm)"); ax.set_ylabel("source-plane y (mm)")
ax.set_title("Isotope separation from a TRUE mixed field, ONE run\n"
             "662 keV window can't reject Co downscatter — but the coded decode puts it at Co's position")
fig.colorbar(im, ax=ax, label="662-window reconstruction", fraction=0.046)
fig.tight_layout()
fig.savefig(os.path.join(here, "mixediso.png"), dpi=130)
print("saved samples/mixediso.png")
