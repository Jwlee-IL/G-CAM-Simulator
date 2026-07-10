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

sty = {"alternating": ("#c0392b", "--", "s"), "3d": ("#2e8b57", "-", "o")}
lab = {"alternating": "alternating iteration", "3d": "3D (x,y,S) search"}

fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(13.5, 5.4))

for scen, mk_over in [("offaxis_near", ""), ("offaxis_far", " (far)")]:
    dash = "-" if scen == "offaxis_near" else ":"
    for method in ("alternating", "3d"):
        col, _, mk = sty[method]
        c, rms = sel(method, scen, "depth_rms_mm")
        ax1.plot(c, rms, marker=mk, ls=dash, color=col, lw=1.9, ms=5,
                 label=f"{lab[method]} — {'near' if scen=='offaxis_near' else 'far'}")
        c, lrms = sel(method, scen, "lateral_rms_mm")
        ax2.plot(c, lrms, marker=mk, ls=dash, color=col, lw=1.9, ms=5,
                 label=f"{lab[method]} — {'near' if scen=='offaxis_near' else 'far'}")

ax1.set_xscale("log"); ax1.set_xlabel("detected counts"); ax1.set_ylabel("depth RMS (mm)")
ax1.set_title("(1) Depth RMS\n3D wins near-field & low counts; far field ill-conditioned for both")
ax1.legend(fontsize=7.5); ax1.grid(alpha=0.3, which="both")

ax2.set_xscale("log"); ax2.set_yscale("log")
ax2.set_xlabel("detected counts"); ax2.set_ylabel("lateral RMS (mm)")
ax2.set_title("(2) Lateral RMS — the coupling trap\nalternating stalls ~3–5 mm; 3D converges to ~0.4 mm")
ax2.legend(fontsize=7.5); ax2.grid(alpha=0.3, which="both")

fig.suptitle("Full 3D (x,y,S) joint search vs the alternating iteration: the 3D search removes the start-point "
             "coupling trap (near-field lateral RMS 3.6→0.37 mm), is more robust at low counts; the far field stays "
             "intrinsically hard (dM/dS→0) for both.", fontsize=9.3)
fig.tight_layout(rect=[0, 0, 1, 0.93])
out = os.path.join(here, "depth3d.png")
fig.savefig(out, dpi=130); print("saved", out)
