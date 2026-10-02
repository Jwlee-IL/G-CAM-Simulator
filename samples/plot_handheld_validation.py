"""Validate the recommended handheld config (16x16 / D55 / GAGG:Mg 15mm) against the
GAGG reference lab geometry (12x12 / D60 / GAGG:Mg 10mm) by MONTE CARLO.

Fair comparison is RMS vs ACQUISITION TIME (not counts): the recommended detector is
~2.5x more efficient, so it banks counts faster. Run first:
  montecarlo noise samples/scenario_handheld.json   samples/noise_handheld.csv
  montecarlo noise samples/scenario_orig_gagg.json  samples/noise_orig_gagg.csv
The curves are one run each (seed 12345); the efficiency ratio and the floors in the annotations are seed-ensemble
means from samples/evidence/results/aggregate.csv (TODO-27).
Usage:  python samples/plot_handheld_validation.py
"""
import os, csv
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))

def load(fn):
    d = {}
    for r in csv.DictReader(open(os.path.join(here, fn))):
        d.setdefault(r["source"], []).append(
            (float(r["detected_counts"]), float(r["rms_error_mm"]),
             float(r["failure_rate"]), float(r["equiv_time_s"])))
    for k in d:
        d[k] = np.array(sorted(d[k]))
    return d

rec = load("noise_handheld.csv")     # 16x16 / D55 / 15mm
org = load("noise_orig_gagg.csv")    # 12x12 / D60 / 10mm

# Seed-ensemble means for the annotations: {key: (mean, N)}.
agg = {r["key"]: (float(r["mean"]), int(r["N"]))
       for r in csv.DictReader(open(os.path.join(here, "evidence", "results", "aggregate.csv")))}
gain = agg["head/ratio"]                                              # efficiency ratio, hand-held / original
floor_rec = agg["noise_head/noise.csv/centered/5000/rms_error_mm"]   # on-axis floor at 5000 counts
floor_org = agg["noise_orig/noise.csv/centered/5000/rms_error_mm"]
eff_rec = agg["precise/scenario_handheld/eff"][0]
eff_org = agg["precise/scenario_orig_gagg/eff"][0]

fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(13.5, 5.4))

# (1) RMS vs acquisition TIME (the fair axis — folds in the efficiency gain)
for cfg, d, col in [("recommended 16×16/D55/15mm", rec, "#2e8b57"),
                    ("original 12×12/D60/10mm", org, "#b8860b")]:
    for src, ls, mk in [("centered", "-", "o"), ("edge_8mm", "--", "s")]:
        a = d[src]; t, rms = a[:, 3], a[:, 1]
        ax1.loglog(t, rms, ls, marker=mk, color=col, lw=1.8, ms=5,
                   label=f"{cfg} — {src.replace('_',' ')}")
ax1.axhline(1.0, color="#888", ls=":", lw=1); ax1.text(0.012, 1.06, "1 mm", color="#888", fontsize=8)
ax1.set_xlabel("acquisition time @ 1 MBq (s)"); ax1.set_ylabel("localization RMS (mm)")
ax1.set_title(f"(1) Fair test: RMS vs TIME — recommended is {gain[0]:.2f}× more efficient\n"
              f"({eff_rec * 1e4:.2f} vs {eff_org * 1e4:.2f} × 1e-4, {gain[1]} seeds) → sub-mm sooner")
ax1.legend(fontsize=7.5); ax1.grid(alpha=0.3, which="both")

# (2) RMS vs detected counts (decoder-limited floor)
for cfg, d, col in [("recommended", rec, "#2e8b57"), ("original", org, "#b8860b")]:
    for src, ls, mk in [("centered", "-", "o"), ("edge_8mm", "--", "s")]:
        a = d[src]; n, rms = a[:, 0], a[:, 1]
        ax2.loglog(n, rms, ls, marker=mk, color=col, lw=1.8, ms=5,
                   label=f"{cfg} — {src.replace('_',' ')}")
ax2.axhline(1.0, color="#888", ls=":", lw=1)
ax2.annotate(f"floor {floor_rec[0]:.2f} mm ({floor_rec[1]} seeds)", (5000, floor_rec[0]), (12, 0.3), fontsize=8,
             color="#2e8b57", arrowprops=dict(arrowstyle="->", color="#2e8b57"))
ax2.annotate(f"floor {floor_org[0]:.2f} mm ({floor_org[1]} seeds)", (5000, floor_org[0]), (900, 0.9), fontsize=8,
             color="#b8860b", arrowprops=dict(arrowstyle="->", color="#b8860b"))
ax2.set_xlabel("detected counts"); ax2.set_ylabel("localization RMS (mm)")
ax2.set_title("(2) RMS vs counts\nbigger array samples the shadow finer → lower floor")
ax2.legend(fontsize=7.5); ax2.grid(alpha=0.3, which="both")

fig.suptitle(f"MC validation of the recommended handheld config: {gain[0]:.2f}× sensitivity, "
             f"{floor_rec[0]:.2f} mm on-axis floor (vs {floor_org[0]:.2f}),\n"
             "sub-mm ≥250 counts (~1.2 s @1MBq). Use non-cyclic decoding for the FOV.", fontsize=11)
fig.tight_layout(rect=[0, 0, 1, 0.94])
out = os.path.join(here, "handheld_validation.png")
fig.savefig(out, dpi=130); print("saved", out)
