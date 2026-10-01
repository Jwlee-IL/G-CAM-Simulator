"""Dose rate from the detector spectrum (run `montecarlo dose samples/scenario_handheld.json`).
(1) estimate / truth vs energy, frontal (fit set, held-out energies, reference sources);
(2) the same vs angle of incidence — the 10 mm mask collimates;
(3) over-range of a paralyzable front end, raw and live-time corrected.
Usage:  python samples/plot_dose.py
"""
import os, csv
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(os.path.join(here, "dose_ratio.csv"))))
over = list(csv.DictReader(open(os.path.join(here, "dose_overrange.csv"))))

fig, axes = plt.subplots(1, 3, figsize=(17, 5.2))

ax = axes[0]
for label, style, name in [("fit", "o-", "fit set"), ("check", "s", "held out")]:
    sel = sorted((float(r["energy_kev"]), float(r["ratio"])) for r in rows if r["label"] == label)
    ax.plot([e for e, _ in sel], [v for _, v in sel], style, label=name)
mean_keV = {"Am-241": 59.5, "Co-57": 123.7, "Ir-192": 380, "Cs-137": 661.7, "Co-60": 1253}
for r in rows:
    if r["label"] in mean_keV:
        e = mean_keV[r["label"]]
        ax.plot(e, float(r["ratio"]), "D", color="#c0392b")
        ax.annotate(r["label"], (e, float(r["ratio"])), textcoords="offset points", xytext=(4, 6), fontsize=8)
ax.axhspan(0.5, 1.5, color="#e8f5e9", zorder=0)
ax.text(55, 1.42, "PR-SAFE-01 band ±50 %", fontsize=8, color="#2e7d32")
ax.set_xscale("log"); ax.set_ylim(0.0, 1.6)
ax.set_xlabel("photon energy (keV)"); ax.set_ylabel("estimated / true H*(10) rate")
ax.set_title("(1) frontal response with the fitted G(E)")
ax.legend(fontsize=8); ax.grid(alpha=0.3, which="both")

ax = axes[1]
for e in sorted({float(r["energy_kev"]) for r in rows if r["label"] == "angle"}):
    sel = sorted((float(r["angle_deg"]), float(r["ratio"])) for r in rows if r["label"] == "angle" and float(r["energy_kev"]) == e)
    ax.plot([a for a, _ in sel], [v for _, v in sel], "o-", ms=3, label=f"{e:.0f} keV")
ax.axhspan(0.5, 1.5, color="#e8f5e9", zorder=0)
ax.axvspan(0, 3.64, color="#eee", zorder=0)
ax.set_xlabel("angle of incidence (°)"); ax.set_ylabel("estimated / true H*(10) rate")
ax.set_title("(2) angular response — the mask collimates")
ax.legend(fontsize=8); ax.grid(alpha=0.3)

ax = axes[2]
h = [float(o["usv_per_h"]) for o in over]
ax.plot(h, [float(o["raw_ratio"]) for o in over], "o-", label="raw (recorded counts)")
ax.plot(h, [float(o["live_corrected_ratio"]) for o in over], "s--", label="live-time corrected")
ax.plot(h, [float(o["live_fraction"]) for o in over], ":", color="#777", label="live fraction")
ax.axhspan(0.5, 1.5, color="#e8f5e9", zorder=0)
ax.axvline(1e4, color="#999", lw=0.8); ax.text(1.1e4, 1.3, "10 mSv/h", fontsize=8)
ax.set_xscale("log"); ax.set_ylim(-0.02, 1.6)
ax.set_xlabel("true dose rate (µSv/h), Cs-137 frontal"); ax.set_ylabel("ratio")
ax.set_title("(3) over-range, paralyzable τ = 1 µs")
ax.legend(fontsize=8); ax.grid(alpha=0.3, which="both")

fig.tight_layout()
out = os.path.join(here, "dose.png")
fig.savefig(out, dpi=110)
print("wrote", out)
