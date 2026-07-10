"""Joint (x,y,S) depth estimation: alternating iteration vs FULL 3D search.
Run `montecarlo depth3d samples/scenario.json` first (writes samples/depth3d.csv).
The 3D search scores each assumed-S slice by its peak prominence (cross-S comparable) and
takes the joint argmax — no nominal-S seed, no lateral<->depth alternation trap.
Usage:  python samples/plot_depth3d.py
"""
import os, csv
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(os.path.join(here, "depth3d.csv"))))
def sel(method, scen, key):
    rs = [r for r in rows if r["method"] == method and r["scenario"] == scen]
    rs.sort(key=lambda r: float(r["counts"]))
    return (np.array([float(r["counts"]) for r in rs]),
            np.array([float(r[key]) for r in rs]))

sty = {"alternating": ("#c0392b", "s", "alt (fixed seed 100)"),
       "alt_oracle":  ("#3b6ea5", "^", "alt (oracle seed = true S)"),
       "3d":          ("#2e8b57", "o", "3D search (seed-free)")}

fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(13.5, 5.4))

for scen in ("offaxis_near", "offaxis_far"):
    dash = "-" if scen == "offaxis_near" else ":"
    tag = "near" if scen == "offaxis_near" else "far"
    for method, (col, mk, lab) in sty.items():
        c, rms = sel(method, scen, "depth_rms_mm")
        ax1.plot(c, rms, marker=mk, ls=dash, color=col, lw=1.8, ms=5, label=f"{lab} — {tag}")
        c, lrms = sel(method, scen, "lateral_rms_mm")
        ax2.plot(c, lrms, marker=mk, ls=dash, color=col, lw=1.8, ms=5, label=f"{lab} — {tag}")

ax1.set_xscale("log"); ax1.set_xlabel("detected counts"); ax1.set_ylabel("depth RMS (mm)")
ax1.set_title("(1) Depth RMS\nthe alternating trap was the SEED: oracle-seeded alt wins far-field (1.6 mm)")
ax1.legend(fontsize=7); ax1.grid(alpha=0.3, which="both")

ax2.set_xscale("log"); ax2.set_yscale("log")
ax2.set_xlabel("detected counts"); ax2.set_ylabel("lateral RMS (mm)")
ax2.set_title("(2) Lateral RMS\n3D is seed-free & robust; well-seeded alt is comparable/better")
ax2.legend(fontsize=7); ax2.grid(alpha=0.3, which="both")

fig.suptitle("Joint (x,y,S) depth, honestly: the alternating iteration's failure was a FIXED bad seed (100), not the "
             "alternation — an oracle-seeded alt converges (far-field 1.6 mm). The 3D search's value is being SEED-FREE, "
             "but its peak-prominence heuristic carries a systematic bias (+7/−18 mm). Best: 3D to seed, alt to refine.",
             fontsize=8.6)
fig.tight_layout(rect=[0, 0, 1, 0.93])
out = os.path.join(here, "depth3d.png")
fig.savefig(out, dpi=130); print("saved", out)
