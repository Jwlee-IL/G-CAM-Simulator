"""SiPM gain temperature drift → energy-window integrity, with stabilization ①+②.

Reference sensor: Hamamatsu S13360-3050CS (samples/sipm/hamamatsu_s13360_3050cs.json).
Question: as temperature swings, how much does the photopeak walk out of the fixed
LLD/ULD energy window, and how far do ① bias-compensation and ② LED-pulser lock fix it?

Key project fact reused: coded-aperture LOCALIZATION is invariant to a global gain scale
(the balanced MURA decode is scale-invariant) — so temperature drift is an ENERGY-window
problem, not a position problem. Only when the peak walks fully out of the window do the
counts (and hence the noise-limited localization) suffer.

Honest residual: ① fixes SiPM V_br(T); ② (LED) also catches SiPM/electronics aging but
BYPASSES the crystal, so GAGG's own light-yield drift (~-0.15%/°C) survives both — only a
spectral reference (K-40 / reference source, ③) removes it.

Usage:  python samples/sipm_thermal_study.py
"""
import os, json
import numpy as np
from math import erf
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
sipm = json.load(open(os.path.join(here, "sipm", "hamamatsu_s13360_3050cs.json"), encoding="utf-8"))
sm = sipm["_stabilizationModel"]

T0 = 20.0                               # calibration temperature (°C)
T = np.linspace(0, 45, 300)            # ambient / board temperature (°C)
WIN = 0.10                              # ±10% LLD/ULD energy window (per-crystal)
FWHM = 0.07                             # GAGG photopeak FWHM at 662 keV
sigma = FWHM / 2.3548                   # relative Gaussian sigma of the peak

# per-°C peak-position tempco for each stabilization case (SiPM part + crystal light yield)
sipm_fixed = sipm["gainTempCoeffPctPerC_fixedBias"] / 100.0          # -1.8%/°C
cry = sm["crystalLightYieldTempCoeffPctPerC"] / 100.0               # -0.15%/°C (only ③ fixes)
cases = {
    "no compensation":       sipm_fixed + cry,                                    # bias fixed
    "① bias comp":           sm["biasCompResidualPctPerC"] / 100.0 + cry,          # SiPM tracked
    "①+② bias + LED lock":   sm["ledLockResidualPctPerC"] / 100.0 + cry,           # + optical gain lock
}
col = {"no compensation": "#c0392b", "① bias comp": "#e67e22", "①+② bias + LED lock": "#2e8b57"}

def retained(peak_shift_rel):
    """Fraction of the Gaussian photopeak still inside the fixed ±WIN window when the
    peak center has moved by peak_shift_rel (relative to the nominal 1.0 line)."""
    lo, hi = 1.0 - WIN, 1.0 + WIN
    mu = 1.0 + peak_shift_rel
    z1 = (hi - mu) / (sigma * np.sqrt(2)); z2 = (lo - mu) / (sigma * np.sqrt(2))
    return 0.5 * (np.vectorize(erf)(z1) - np.vectorize(erf)(z2))

R0 = retained(0.0)   # at calibration T, ~full window capture

# ③ spectral stabilization: NO temperature slope (it tracks the real scintillation line, so it
# also catches crystal light-yield). Its residual is a random centroid jitter from reference
# statistics + a small energy-extrapolation term — set by counts, not temperature.
def sigma3(ref_rate_cps, t_update_s):
    n = np.maximum(ref_rate_cps * t_update_s, 1e-9)
    stat = (sm3["spectralRefFwhmPct"] / 100.0) / 2.3548 / np.sqrt(n)   # relative centroid error
    extrap = sm3["spectralRefExtrapResidualPct"] / 100.0
    return np.hypot(stat, extrap)

sm3 = sipm["_stabilizationModel"]
REF_RATE, T_UPD = 300.0, 10.0        # a built-in reference source: ~300 cps, 10 s update
s3 = sigma3(REF_RATE, T_UPD)
def retained_jitter(sig):            # window counts averaged over a Gaussian peak jitter of std sig
    d = np.linspace(-4 * sig, 4 * sig, 41) if sig > 0 else np.array([0.0])
    wt = np.exp(-0.5 * (d / sig) ** 2) if sig > 0 else np.array([1.0])
    return np.sum(wt * retained(d)) / np.sum(wt)

fig, (ax1, ax2, ax3) = plt.subplots(1, 3, figsize=(17.5, 5.6))

# (1) peak-position error (% of 662) vs temperature — ①/② have a residual slope, ③ is flat
for name, tc in cases.items():
    ax1.plot(T, (tc * (T - T0)) * 100.0, color=col[name], lw=2.2, label=name)
ax1.plot(T, np.zeros_like(T), color="#8e44ad", lw=2.4, label="③ spectral stabilization")
ax1.fill_between(T, -s3*100, s3*100, color="#8e44ad", alpha=0.15)
ax1.axhspan(-WIN*100, WIN*100, color="#3498db", alpha=0.08)
ax1.text(1, WIN*100*0.8, f"±{WIN*100:.0f}% window", color="#2471a3", fontsize=8)
ax1.axvline(T0, ls=":", color="#888")
ax1.set_xlabel("temperature (°C)"); ax1.set_ylabel("photopeak position error (% of 662 keV)")
ax1.set_title("(1) Peak walk vs T\n③ has NO T-slope (tracks the real line → crystal too)")
ax1.legend(fontsize=7.5); ax1.grid(alpha=0.3)

# (2) window-retained counts (%) vs temperature
for name, tc in cases.items():
    ax2.plot(T, retained(tc * (T - T0)) / R0 * 100.0, color=col[name], lw=2.2, label=name)
ax2.plot(T, np.full_like(T, retained_jitter(s3) / R0 * 100.0), color="#8e44ad", lw=2.4,
         label=f"③ spectral (flat, σ={s3*100:.2f}%)")
ax2.axhline(100, ls=":", color="#888")
ax2.annotate("localization RMS: flat\n(gain-scale invariant — position, not energy)",
             (23, 50), (5, 34), fontsize=7.5, color="#555",
             arrowprops=dict(arrowstyle="->", color="#555"))
ax2.set_xlabel("temperature (°C)"); ax2.set_ylabel("counts kept in the energy window (%)")
ax2.set_title("(2) Window integrity vs T\n①+② slope with crystal LY; ③ removes even that")
ax2.legend(fontsize=7.5, loc="lower center"); ax2.grid(alpha=0.3); ax2.set_ylim(0, 105)

# (3) ③'s OWN limit — a counts axis, not temperature: stabilization precision vs reference rate
rate = np.logspace(-2, 3.5, 200)
for tu, ls in [(10.0, "-"), (60.0, "--")]:
    ax3.loglog(rate, sigma3(rate, tu) * 100.0, ls, color="#8e44ad", lw=2, label=f"update {tu:.0f}s")
ax3.axhline(sm3["spectralRefExtrapResidualPct"], ls=":", color="#8e44ad")
ax3.text(0.02, sm3["spectralRefExtrapResidualPct"]*1.1, "extrapolation floor 0.3%", fontsize=7.5, color="#8e44ad")
ax3.axhline(2.0, color="#27ae60", lw=1.2); ax3.text(0.02, 2.15, "target <2% (window-safe)", fontsize=8, color="#27ae60")
for lbl, r, cc in [("K-40 bkg\n~0.05 cps", 0.05, "#c0392b"), ("built-in\nAm-241 ~300 cps", 300.0, "#2e8b57")]:
    ax3.axvline(r, ls="-.", color=cc, lw=1); ax3.text(r*1.15, 6, lbl, fontsize=7, color=cc, rotation=0)
ax3.set_xlabel("reference line count rate (cps)"); ax3.set_ylabel("③ gain-stabilization jitter σ (%)")
ax3.set_title("(3) ③ is limited by COUNTS, not temperature\nGAGG has no intrinsic line → needs a real reference")
ax3.legend(fontsize=8); ax3.grid(alpha=0.3, which="both"); ax3.set_ylim(0.1, 30)

fig.suptitle("SiPM gain drift (S13360-3050CS): ① bias-comp + ② LED hold the window but leave the crystal light-yield "
             "slope; ③ spectral stabilization removes even that — at the cost of a reference source (GAGG has no self-line).", fontsize=10)
fig.tight_layout(rect=[0, 0, 1, 0.94])
out = os.path.join(here, "sipm_thermal.png")
fig.savefig(out, dpi=130); print("saved", out)
print(f"\n③ spectral: ref {REF_RATE:.0f} cps, {T_UPD:.0f}s update → σ = {s3*100:.2f}% (flat vs T); "
      f"K-40 0.05 cps/60s → σ = {sigma3(0.05,60)*100:.1f}% (too weak)")

# text: how far can T drift before a ±10% window loses 10% of counts?
print(f"\nSiPM: {sipm['name']}  fixed-bias gain tempco {sipm['gainTempCoeffPctPerC_fixedBias']}%/°C")
print(f"±{WIN*100:.0f}% window, {FWHM*100:.0f}% FWHM peak, calib {T0}°C")
for name, tc in cases.items():
    # find |ΔT| where retained drops to 90% of R0
    dT = np.linspace(0, 60, 6000)
    keep = retained(tc * dT) / R0
    idx = np.argmax(keep < 0.90)
    lim = dT[idx] if keep[-1] < 0.90 else float("inf")
    print(f"  {name:22s}: tempco {tc*100:+.2f}%/°C  ->  10% count loss at ΔT = {lim if lim!=float('inf') else '>60'}{'°C' if lim!=float('inf') else ''}")
