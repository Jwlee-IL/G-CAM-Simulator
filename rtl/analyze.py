"""Score the RTL peak-detector output (peaks.txt) against the event truth
(truth.csv): detection efficiency, photopeak energy resolution, pile-up loss.
Also plots a waveform slice with detected peaks and the reconstructed spectrum.

Usage: python analyze.py [outdir] [label]
"""
import sys
import csv
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

outdir = sys.argv[1] if len(sys.argv) > 1 else "."
label  = sys.argv[2] if len(sys.argv) > 2 else "run"

ADC_PER_KEV = 3000.0 / 662.0
MATCH_WIN = 10  # samples

truth = list(csv.DictReader(open(f"{outdir}/truth.csv")))
tt = np.array([int(r["sample"]) for r in truth])
te = np.array([float(r["energy_keV"]) for r in truth])

det = np.loadtxt(f"{outdir}/peaks.txt")
if det.size == 0:
    print("no peaks detected"); sys.exit(0)
if det.ndim == 1:
    det = det.reshape(-1, 2)
dt = det[:, 0].astype(int)
da = det[:, 1]
det_keV = da / ADC_PER_KEV

# Greedy nearest-time match (one truth event per detected peak).
order = np.argsort(dt)
used = np.zeros(len(tt), dtype=bool)
n_matched = 0
for i in order:
    j = int(np.argmin(np.abs(tt - dt[i])))
    if abs(tt[j] - dt[i]) <= MATCH_WIN and not used[j]:
        used[j] = True
        n_matched += 1

eff = n_matched / len(tt)
false = len(dt) - n_matched
near = det_keV[(det_keV > 600) & (det_keV < 720)]
fwhm = (2.355 * near.std() / near.mean() * 100) if len(near) > 5 else float("nan")

print(f"[{label}] events={len(tt)} detected={len(dt)} matched={n_matched} "
      f"eff={eff*100:.1f}% false/pileup={false} photopeak_FWHM={fwhm:.1f}%")

# --- plots ---
adc = np.loadtxt(f"{outdir}/adc.txt")
fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(12, 4.5))

# Waveform slice with detected peaks.
lo, hi = 400, min(len(adc), 400 + 2500)
ax1.plot(np.arange(lo, hi), adc[lo:hi], lw=0.6, color="#3b6ea5")
in_slice = (dt >= lo) & (dt < hi)
ax1.plot(dt[in_slice], adc[dt[in_slice]], "v", color="#c0392b", ms=6, label="RTL peak")
ax1.set_xlabel("sample (10 ns each)"); ax1.set_ylabel("ADC counts")
ax1.set_title(f"ADC waveform + detected peaks ({label})"); ax1.legend(fontsize=8)

# Reconstructed vs true energy spectrum.
bins = np.linspace(0, 800, 80)
ax2.hist(te, bins=bins, histtype="step", color="gray", label="true events")
ax2.hist(det_keV, bins=bins, histtype="stepfilled", alpha=0.5, color="#c0392b",
         label="RTL-reconstructed")
ax2.axvline(662, ls=":", color="k", lw=1); ax2.text(665, ax2.get_ylim()[1]*0.9, "662 keV", fontsize=8)
ax2.set_xlabel("energy (keV)"); ax2.set_ylabel("counts")
ax2.set_title(f"Reconstructed Cs-137 spectrum (eff {eff*100:.0f}%, FWHM {fwhm:.1f}%)")
ax2.legend(fontsize=8)

fig.tight_layout()
fig.savefig(f"{outdir}/rtl_{label}.png", dpi=130)
print(f"saved {outdir}/rtl_{label}.png")
