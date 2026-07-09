"""Plot the mask-channel geometry study (run `montecarlo maskgeo samples/scenario.json`):
hole size (efficiency vs resolution), focused-channel depth of field, and off-axis uniformity.

Usage:  python samples/plot_maskgeo.py
"""
import os
import csv
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(os.path.join(here, "maskgeo.csv"))))

def rows_of(exp):
    return [r for r in rows if r["experiment"] == exp]

fig, (ax1, ax2, ax3) = plt.subplots(1, 3, figsize=(16, 4.8))

# (A) hole size: efficiency + resolution vs hole fraction
hs = sorted(rows_of("holesize"), key=lambda r: float(r["x"]))
hf = [float(r["x"]) for r in hs]
eff = [float(r["efficiency"]) * 1e4 for r in hs]
rms = [float(r["rms_mm"]) for r in hs]
ax1.plot(hf, eff, "o-", color="#3b6ea5", lw=2, label="efficiency (×1e-4)")
ax1.set_xlabel("hole fraction (open area of a cell)")
ax1.set_ylabel("geometric efficiency (×1e-4)", color="#3b6ea5")
ax1.set_title("(A) Hole size: smaller holes lose BOTH\nsensitivity and resolution (pinhole intuition fails)")
axr = ax1.twinx()
axr.plot(hf, rms, "s--", color="#c0392b", lw=2, label="localization RMS")
axr.set_ylabel("localization RMS @ fixed counts (mm)", color="#c0392b")
ax1.grid(alpha=0.3)

# (B) depth of field: focused/straight efficiency ratio vs source distance
dof = rows_of("dof")
S = sorted({float(r["x"]) for r in dof})
straight = {float(r["x"]): float(r["efficiency"]) for r in dof if r["series"] == "straight"}
focused = {float(r["x"]): float(r["efficiency"]) for r in dof if r["series"] == "focused"}
ratio = [focused[s] / straight[s] for s in S]
ax2.plot(S, ratio, "o-", color="#8e44ad", lw=2)
ax2.axhline(1.0, ls=":", color="gray")
ax2.axvline(100, ls="--", color="#27ae60", lw=1); ax2.text(102, min(ratio), "focal", color="#27ae60", fontsize=9)
ax2.set_xlabel("source distance S (mm)")
ax2.set_ylabel("focused / straight efficiency")
ax2.set_title("(B) Focused channels (25 mm mask): efficiency\npeaks at the focal distance — depth of field")
ax2.grid(alpha=0.3)

# (C) off-axis uniformity: edge/center for straight vs focused
oa = rows_of("offaxis")
geoms = ["straight", "focused"]
ratios = []
for g in geoms:
    c = float(next(r for r in oa if r["x"] == g and r["series"] == "center")["efficiency"])
    e = float(next(r for r in oa if r["x"] == g and r["series"] == "edge")["efficiency"])
    ratios.append(e / c)
bars = ax3.bar(geoms, ratios, color=["#4C9F70", "#c0392b"], width=0.5)
ax3.axhline(1.0, ls=":", color="gray")
for b, r in zip(bars, ratios):
    ax3.text(b.get_x() + b.get_width() / 2, r, f"{r:.2f}", ha="center", va="bottom", fontsize=10)
ax3.set_ylabel("edge / center efficiency  (1 = uniform FOV)")
ax3.set_title("(C) Off-axis (25 mm mask): point-focusing NARROWS\nthe FOV — the edge source isn't at the focal point")
ax3.set_ylim(0, 1.1)

fig.suptitle("Mask channel geometry: hole size (never helps a coded aperture) and focused channels "
             "(a focal-point concentrator, not a uniform gain)", fontsize=12)
fig.tight_layout(rect=[0, 0, 1, 0.95])
fig.savefig(os.path.join(here, "maskgeo.png"), dpi=130)
print("saved samples/maskgeo.png")
