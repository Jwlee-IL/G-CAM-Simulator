"""Validate the recommended handheld config (16x16 / D55 / GAGG:Mg 15mm) against the
original-ish rig (12x12 / D60 / GAGG:Mg 10mm) by MONTE CARLO.

Fair comparison is RMS vs ACQUISITION TIME (not counts): the recommended detector is
~2.45x more efficient, so it banks counts faster. Run first:
  montecarlo noise samples/scenario_handheld.json   samples/noise_handheld.csv
  montecarlo noise samples/scenario_orig_gagg.json  samples/noise_orig_gagg.csv
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

rec = load("noise_handheld.csv")     # 16x16 / D55 / 15mm  eff 2.65e-4
org = load("noise_orig_gagg.csv")    # 12x12 / D60 / 10mm  eff 1.08e-4

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
ax1.set_title("(1) Fair test: RMS vs TIME\nrecommended is ~2.45× more efficient → sub-mm sooner")
ax1.legend(fontsize=7.5); ax1.grid(alpha=0.3, which="both")

# (2) RMS vs detected counts (decoder-limited floor)
for cfg, d, col in [("recommended", rec, "#2e8b57"), ("original", org, "#b8860b")]:
    for src, ls, mk in [("centered", "-", "o"), ("edge_8mm", "--", "s")]:
        a = d[src]; n, rms = a[:, 0], a[:, 1]
        ax2.loglog(n, rms, ls, marker=mk, color=col, lw=1.8, ms=5,
                   label=f"{cfg} — {src.replace('_',' ')}")
ax2.axhline(1.0, color="#888", ls=":", lw=1)
ax2.annotate("floor 0.34 mm", (250, 0.34), (300, 0.20), fontsize=8, color="#2e8b57",
             arrowprops=dict(arrowstyle="->", color="#2e8b57"))
ax2.annotate("floor 0.55 mm", (1000, 0.55), (900, 0.9), fontsize=8, color="#b8860b",
             arrowprops=dict(arrowstyle="->", color="#b8860b"))
ax2.set_xlabel("detected counts"); ax2.set_ylabel("localization RMS (mm)")
ax2.set_title("(2) RMS vs counts\nbigger array samples the shadow finer → lower floor")
ax2.legend(fontsize=7.5); ax2.grid(alpha=0.3, which="both")

fig.suptitle("MC validation of the recommended handheld config: ~2.45× sensitivity, 0.34 mm floor (vs 0.55), "
             "sub-mm ≥250 counts (~1 s @1MBq). Use non-cyclic decoding for the FOV.", fontsize=11)
fig.tight_layout(rect=[0, 0, 1, 0.94])
out = os.path.join(here, "handheld_validation.png")
fig.savefig(out, dpi=130); print("saved", out)
