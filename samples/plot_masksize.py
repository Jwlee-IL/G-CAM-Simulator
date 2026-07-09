"""The two mask-SIZE optima (run `montecarlo masksize samples/scenario.json` first):
  (1) cell (feature) size  -- measured localization RMS vs cell pitch (masksize.csv)
  (2) open fraction        -- analytical coded-aperture SNR vs open fraction rho

Usage:  python samples/plot_masksize.py
"""
import os
import csv
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
rows = list(csv.DictReader(open(os.path.join(here, "masksize.csv"))))
pitch = np.array([float(r["cell_pitch_mm"]) for r in rows])
shadow = np.array([float(r["shadow_per_pixel"]) for r in rows])
rms = np.array([float(r["rms_mm"]) for r in rows])
res = np.array([float(r["resolution_mm"]) for r in rows])

fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(14, 5.4))

# (1) cell size: RMS vs cell pitch (shadow/pixel on the top axis)
ax1.semilogy(pitch, rms, "o-", color="#3b6ea5", lw=2, label="localization RMS")
ax1.semilogy(pitch, res, "s--", color="#95a5a6", lw=1.3, label="resolution (proj. cell)")
# shade the usable band (sub-mm RMS): shadow ~ 0.8..2.5 px
band = (shadow > 0.75) & (shadow < 2.6)
ax1.axvspan(pitch[band].min(), pitch[band].max(), color="#2ecc71", alpha=0.12)
ax1.text(pitch[band].mean(), 0.4, "sweet band\n(shadow 0.8–2.5 px)", ha="center", fontsize=8, color="#27ae60")
ax1.text(0.4, 3, "aliasing\n(shadow<1px)", fontsize=7.5, color="#c0392b")
ax1.text(2.4, 12, "mask overflows\ndetector", fontsize=7.5, color="#c0392b", ha="center")
ax1.set_xlabel("mask cell pitch (mm)"); ax1.set_ylabel("localization RMS @ fixed counts (mm)")
ax1.set_title("(1) Cell size: a bounded optimum\n(too fine → aliasing; too coarse → mask > detector)")
ax1.legend(fontsize=8); ax1.grid(alpha=0.3, which="both")
axt = ax1.twiny(); axt.set_xlim(ax1.get_xlim())
axt.set_xticks(pitch); axt.set_xticklabels([f"{sh:.1f}" for sh in shadow], fontsize=7)
axt.set_xlabel("cell shadow width (detector pixels)", fontsize=8)

# (2) open fraction: analytical coded-aperture point-source SNR vs rho
# SNR(rho) = Phi_s * sqrt(rho(1-rho)) / sqrt((1-rho)Phi_s + rho*Phi_b + B_det)
rho = np.linspace(0.02, 0.98, 200)
Phi_s = 1.0
# SNR = Phi_s*sqrt(rho(1-rho)) / sqrt((1-rho)Phi_s + rho*Phi_b + B_det); the optimum rho depends
# on which denominator term dominates: detector bg -> 0.5, aperture bg -> <0.5, source noise -> >0.5.
for label, Phi_b, B_det, col in [("detector bg (ρ-indep) → 0.5", 0.0, 20.0, "#c0392b"),
                                 ("aperture bg (ρΦ_b) → <0.5", 20.0, 0.02, "#3b6ea5"),
                                 ("source noise → >0.5", 0.0, 0.02, "#2ecc71")]:
    snr = Phi_s * np.sqrt(rho * (1 - rho)) / np.sqrt((1 - rho) * Phi_s + rho * Phi_b + B_det)
    rho_opt = rho[np.argmax(snr)]
    ax2.plot(rho, snr / snr.max(), "-", color=col, lw=2, label=f"{label}  (ρ*={rho_opt:.2f})")
ax2.axvline(0.5, ls="--", color="#e67e22", lw=1.5); ax2.text(0.51, 0.05, "MURA ≈ 50%", color="#e67e22", fontsize=9)
ax2.set_xlabel("mask open fraction ρ")
ax2.set_ylabel("point-source SNR (normalized)")
ax2.set_title("(2) Open fraction: coding power ∝ √(ρ(1−ρ)); the optimum ρ\nshifts with the dominant noise — MURA's 50% fits detector-bg")
ax2.legend(fontsize=8); ax2.grid(alpha=0.3)

fig.suptitle("The two mask-size optima: cell size has a bounded sweet spot; open-fraction optimum "
             "shifts with the dominant noise (MURA's 50% fits the detector-bg-limited case)", fontsize=12)
fig.tight_layout(rect=[0, 0, 1, 0.95])
fig.savefig(os.path.join(here, "masksize.png"), dpi=130)
print("saved samples/masksize.png")
