"""Two field-hardening problems of SiPM-based coded-aperture cameras:
  (1) DAQ/ADC board HEAT — SiPM gain drifts with temperature, so electronics heat matters.
  (2) MOTION — coded-aperture decoding assumes a steady pointing; motion blurs the shadow.

Both are analysed here against this project's numbers.

MOTION: coded-aperture decoding needs the mask shadow to land at a stable detector
position while counts accumulate. A pointing rotation δφ shifts the shadow by D·δφ.
Keep the smear under ~half a cell shadow (~0.8 mm) => δφ_tol ≈ atan(0.8/55) ≈ 0.8°.
Whether that's a problem depends on how long you must integrate = 200 counts / rate.

THERMAL: heat scales with channel count (fast ADC + FPGA per channel). Monolithic
(~30 ch) dissipates far less than a pixel array (~256 ch). The 2.5 kg tungsten shield
is also a large heat sink / thermal mass (c≈134 J/kgK, k≈170 W/mK) — the mass carried
for shielding doubles as a cooler.

Usage:  python samples/thermal_motion_study.py
"""
import os
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
D = 55.0                                   # standoff mm
tol_deg = np.degrees(np.arctan(0.8 / D))   # pointing tolerance ~0.8°
COUNTS = 200                               # counts for a sub-mm fix (from the noise study)

fig, (axM, axT) = plt.subplots(1, 2, figsize=(14.5, 5.8))

# ================= (A) MOTION =================
rate = np.logspace(0, 3, 200)              # detected count rate at the head (cps)
t_need = COUNTS / rate                     # integration time to reach 200 counts (s)
axM.loglog(rate, t_need, color="#2c3e50", lw=2.2, label=f"time to {COUNTS} counts")

# drift reaches the tolerance at t = tol/drift_rate for each operator case
for name, drift, col in [("handheld free (~1°/s)", 1.0, "#c0392b"),
                         ("braced/2-hand (~0.2°/s)", 0.2, "#e67e22"),
                         ("monopod (~0.05°/s)", 0.05, "#27ae60")]:
    t_lim = tol_deg / drift
    axM.axhline(t_lim, ls="--", color=col, lw=1.5)
    axM.text(1.2, t_lim * 1.12, f"{name}: smears past {t_lim:.1f}s", color=col, fontsize=8)
axM.axhspan(tol_deg / 1.0, 1e3, color="#c0392b", alpha=0.06)
# with per-event IMU de-rotation the time limit lifts (residual ~arcmin) -> down-arrow
axM.annotate("IMU list-mode\nde-rotation lifts\nthe motion limit",
             (300, 0.7), (60, 8), fontsize=8, color="#2471a3",
             arrowprops=dict(arrowstyle="->", color="#2471a3"))
axM.set_xlabel("detected count rate at the head (cps)")
axM.set_ylabel("integration time (s)")
axM.set_title(f"(A) Motion: shadow smears if you integrate past the drift limit\n"
              f"pointing tolerance δφ≈{tol_deg:.1f}° (½ cell shadow / D={D:.0f}mm)")
axM.legend(fontsize=8, loc="upper right"); axM.grid(alpha=0.3, which="both")
axM.set_ylim(0.05, 1e3)

# ================= (B) THERMAL =================
# steady-state temperature rise over ambient for passive housing rejection
C_shield = 2.5 * 134.0                      # tungsten heat capacity, J/K
cases = {"monolithic ~30ch": 4.0, "pixel ~256ch": 12.0}
h_passive = 0.5                             # W/K, natural convection off a small sealed housing
h_finned = 1.5                              # W/K, finned / light forced convection

x = np.arange(len(cases)); w = 0.35
for i, (h, lab, col) in enumerate([(h_passive, "sealed passive", "#95a5a6"),
                                   (h_finned, "finned/airflow", "#3498db")]):
    dT = [P / h for P in cases.values()]
    axT.bar(x + (i - 0.5) * w, dT, w, color=col, label=lab)
    for xi, v in zip(x + (i - 0.5) * w, dT):
        axT.text(xi, v + 0.5, f"+{v:.0f}°C", ha="center", fontsize=8)
axT.axhline(15, ls="--", color="#c0392b", lw=1.4)
axT.text(1.4, 16, "comfort/gain-drift ceiling ~+15°C", color="#c0392b", fontsize=8, ha="right")
axT.set_xticks(x); axT.set_xticklabels(cases.keys())
axT.set_ylabel("steady-state ΔT over ambient (°C)")
axT.set_title("(B) DAQ heat: fewer channels → far less heat\n"
              "monolithic runs passive; pixel needs fins/airflow")
axT.legend(fontsize=8); axT.grid(axis="y", alpha=0.3)

# annotate the shield-as-thermal-mass buffer for short missions
txt = (f"Shield thermal mass buffers transients:\n"
       f"2.5 kg W = {C_shield:.0f} J/K → a {list(cases.values())[0]:.0f} W board for 60 s\n"
       f"raises it only +{4.0*60/C_shield:.1f}°C (route DAQ heat INTO the shield).")
axT.text(0.5, -0.32, txt, transform=axT.transAxes, ha="center", va="top", fontsize=8,
         bbox=dict(boxstyle="round", fc="#f6f6f6", ec="#bbb"))

fig.suptitle("Field hardening: motion is fixed by an IMU + list-mode de-rotation (not a heavy gimbal); "
             "DAQ heat is cut by monolithic readout + using the tungsten shield as a heat sink.", fontsize=10.5)
fig.tight_layout(rect=[0, 0.03, 1, 0.94])
out = os.path.join(here, "thermal_motion.png")
fig.savefig(out, dpi=130); print("saved", out)

print(f"\npointing tolerance = {tol_deg:.2f}°  (shadow smear < 0.8 mm at D={D:.0f}mm)")
print("motion-limited count rate (need compensation below):")
for name, drift in [("handheld 1°/s", 1.0), ("braced 0.2°/s", 0.2), ("monopod 0.05°/s", 0.05)]:
    r = COUNTS / (tol_deg / drift)
    print(f"  {name:16s}: smears when rate < {r:5.0f} cps  (integration > {tol_deg/drift:.1f}s)")
print(f"\nshield heat buffer: 2.5 kg W = {C_shield:.0f} J/K; 4 W·60s → +{4*60/C_shield:.1f}°C, 12 W·60s → +{12*60/C_shield:.1f}°C")
