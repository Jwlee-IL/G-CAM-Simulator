"""Per-pixel Compton stripping on a TRUE mixed field, CO-LOCATED case (run
`montecarlo mixedstrip samples/scenario.json`). Cs-137 and a strong Co-60 at the SAME
position: the 662-window reconstruction is inflated +220% by Co downscatter (spatial decode
can't separate co-located sources), but per-pixel stripping (subtract R x the Co-photopeak
image) recovers the true Cs count. The spectral lever succeeds where the spatial one can't.
Usage:  python samples/plot_mixedstrip.py
"""
import os, csv
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
def load(fn):
    rows = list(csv.DictReader(open(os.path.join(here, fn))))
    xs = np.array([float(r["x_mm"]) for r in rows]); ys = np.array([float(r["y_mm"]) for r in rows])
    vs = np.array([float(r["value"]) for r in rows])
    ux, uy = np.unique(xs), np.unique(ys)
    return ux, uy, vs.reshape(len(uy), len(ux))

ux, uy, raw = load("mixedstrip_raw.csv")
_, _, strip = load("mixedstrip_stripped.csv")
vmax = raw.max()

fig, (a1, a2) = plt.subplots(1, 2, figsize=(12.5, 5.4))
for ax, g, title in [(a1, raw, "raw 662 window  (+223% over-count)"),
                     (a2, strip, "after per-pixel stripping  (~true Cs)")]:
    im = ax.imshow(g, origin="lower", extent=[ux.min(), ux.max(), uy.min(), uy.max()],
                   cmap="magma", vmin=0, vmax=vmax, aspect="equal")
    ax.plot(0, 0, "o", mfc="none", mec="#2ecc71", ms=20, mew=2.5)
    ax.set_xlabel("x (mm)"); ax.set_ylabel("y (mm)"); ax.set_title(title, fontsize=10)
    fig.colorbar(im, ax=ax, fraction=0.046)
a1.text(0.8, 1.2, "Cs+Co co-located", color="#2ecc71", fontsize=8, weight="bold")

fig.suptitle("Co-located Cs-137 + Co-60 (activity ×8), 662-window image from a TRUE mixed field: spatial decode "
             "can't separate them, but per-pixel Compton stripping recovers the true Cs (spectral lever).", fontsize=9.5)
fig.tight_layout(rect=[0, 0, 1, 0.95])
fig.savefig(os.path.join(here, "mixedstrip.png"), dpi=130)
print("saved samples/mixedstrip.png")
