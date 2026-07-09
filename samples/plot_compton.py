"""Plot the crystal-Compton study outputs (run `montecarlo compton samples/scenario.json` first):
  - compton_strategies.csv     -> efficiency vs localization RMS per positioning strategy
  - compton_recon_{cs,co,combined}.csv -> the multi-isotope 662-window reconstructions

Usage:  python samples/plot_compton.py
"""
import os
import csv
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))


def load_recon(path):
    with open(path) as f:
        header = f.readline()
    meta = dict(kv.split("=") for kv in header.replace("#", "").split())
    grid = np.loadtxt(path, delimiter=",", skiprows=1)
    origin, step = float(meta["origin_mm"]), float(meta["step_mm"])
    return grid, origin, step


# --- 1. strategy tradeoff ---
rows = list(csv.DictReader(open(os.path.join(here, "compton_strategies.csv"))))
fig, ax = plt.subplots(figsize=(8, 5.5))
colors = {"Ideal (no Compton)": "#000000", "PerPixelWindow": "#c0392b",
          "AntiCoincidence": "#e67e22", "Argmax": "#4C9F70", "Centroid": "#8064A2"}
for r in rows:
    name = r["strategy"]
    eff = float(r["efficiency_rel"]) * 100
    rms = float(r["rms_mm"])
    mk = "*" if name.startswith("Ideal") else "o"
    ax.scatter(rms, eff, s=180 if mk == "*" else 120, marker=mk,
               color=colors.get(name, "#3b6ea5"), zorder=3,
               edgecolor="k", linewidth=0.5, label=name)
    ax.annotate(name, (rms, eff), fontsize=8, textcoords="offset points", xytext=(8, 4))
ax.set_xlabel("localization RMS (mm)  ← better")
ax.set_ylabel("efficiency: accepted / ideal (%)  better ↑")
ax.set_title("Crystal Compton: multi-pixel positioning strategies\n"
             "(Argmax recovers Compton-split full-energy events → more counts, sharp position)")
ax.grid(alpha=0.3)
fig.tight_layout()
fig.savefig(os.path.join(here, "compton_strategies.png"), dpi=130)
print("saved samples/compton_strategies.png")

# --- 2. multi-isotope contamination reconstructions ---
specs = [("compton_recon_cs.csv", "Cs-137 window @ (4,0)\n(true source)", (4, 0)),
         ("compton_recon_co.csv", "Co-60 downscatter into\nthe 662 window @ (-5,3)", (-5, 3)),
         ("compton_recon_combined.csv", "Combined 662 window\n→ decode shows BOTH", None)]
fig2, axes = plt.subplots(1, 3, figsize=(15, 4.8))
for ax, (fname, title, truth) in zip(axes, specs):
    grid, origin, step = load_recon(os.path.join(here, fname))
    n = grid.shape[0]
    extent = [origin, origin + step * (n - 1), origin, origin + step * (n - 1)]
    im = ax.imshow(grid, origin="lower", extent=extent, cmap="inferno", aspect="equal")
    for (tx, ty), lab, col in [((4, 0), "Cs", "#4dd0e1"), ((-5, 3), "Co", "#ff5252")]:
        ax.plot(tx, ty, "+", color=col, ms=13, mew=2)
        ax.text(tx + 0.6, ty + 0.6, lab, color=col, fontsize=9, fontweight="bold")
    ax.set_title(title, fontsize=10)
    ax.set_xlabel("x (mm)"); ax.set_ylabel("y (mm)")
    fig2.colorbar(im, ax=ax, fraction=0.046)
fig2.suptitle("Multi-isotope: energy windowing can't remove Co-60 downscatter, but the coded decode separates it spatially",
              fontsize=12)
fig2.tight_layout(rect=[0, 0, 1, 0.96])
fig2.savefig(os.path.join(here, "compton_contamination.png"), dpi=130)
print("saved samples/compton_contamination.png")
