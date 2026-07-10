"""Compton stripping: the SPECTRAL complement to the coded-aperture spatial separation
(theme 15). A higher line's Compton continuum spills into a lower line's energy window, so a
raw window count over-estimates the lower isotope -- "high sensitivity but the counts are
another isotope's downscatter". Stripping models each line's continuum (from its photopeak
area x the Klein-Nishina shape) and subtracts it, top-down, recovering each line's true NET
count. Spatial decode recovers WHERE; stripping recovers HOW MANY.

Pure spectral algorithm (no RTL): build a Cs-137 + Co-60 + Co-57 spectrum with known net
photopeak counts, strip, and compare raw-window vs stripped vs truth.

Usage:  python rtl/compton_stripping_study.py
"""
import os
import math
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

os.chdir(os.path.dirname(os.path.abspath(__file__)))
rng = np.random.default_rng(20260709)

# photopeak fraction (full-energy prob) vs energy -- same model as multi_isotope_study.py
def photofraction(E):
    return float(np.clip(0.93 * (150.0 / E) ** 0.55, 0.12, 0.95))

def compton_edge(E):
    a = E / 511.0
    return E * 2.0 * a / (1.0 + 2.0 * a)

def sample_compton(E, n):
    """Klein-Nishina electron (deposited) energy, 0..edge."""
    a = E / 511.0
    Emin = E / (1.0 + 2.0 * a)
    Ep = np.linspace(Emin, E, 400)
    cos = 1.0 - 511.0 * (1.0 / Ep - 1.0 / E)
    sin2 = np.clip(1.0 - cos * cos, 0.0, 1.0)
    w = np.clip(Ep / E + E / Ep - sin2, 0.0, None)
    cdf = np.cumsum(w); cdf /= cdf[-1]
    return E - np.interp(rng.random(n), cdf, Ep)

# --- spectrum model: (name, energy, TRUE net photopeak counts) ---
LINES = [("Co-60", 1332.5, 6000), ("Co-60", 1173.2, 6000),
         ("Cs-137", 661.7, 8000), ("Co-57", 122.1, 10000)]
RES = 0.07                      # 7% FWHM at 662, scales as 1/sqrt(E)
NBIN = 700
EMAX = 1600.0
edges = np.linspace(0, EMAX, NBIN + 1)
ctr = 0.5 * (edges[:-1] + edges[1:])

def gauss_res_sigma(E):
    return RES * 662.0 / 2.355 * np.sqrt(E / 662.0)

# build the measured spectrum: each line = photopeak Gaussian + its KN continuum
spec = np.zeros(NBIN)
truth_net = {}
for name, E, P in LINES:
    truth_net[(name, E)] = P
    # photopeak
    pk = rng.normal(E, gauss_res_sigma(E), P)
    spec += np.histogram(pk, bins=edges)[0]
    # continuum: area = P * (1-pf)/pf
    pf = photofraction(E)
    ncont = int(P * (1.0 - pf) / pf)
    dep = sample_compton(E, ncont)
    dep += rng.normal(0, gauss_res_sigma(E) * 0.5, ncont)   # mild broadening
    spec += np.histogram(dep, bins=edges)[0]
spec = np.clip(spec + rng.normal(0, np.sqrt(np.clip(spec, 1, None))), 0, None)  # Poisson-ish noise

# normalized continuum shape per line energy (for the stripper's model)
def continuum_shape(E):
    dep = sample_compton(E, 200000)
    h = np.histogram(dep, bins=edges)[0].astype(float)
    s = h.sum()
    return h / s if s > 0 else h

WF = 0.10
def window_mask(E):
    return (ctr > E * (1 - WF)) & (ctr < E * (1 + WF))

def net_area(s, E):
    """Photopeak net area: gross window minus a FLAT baseline = the MINIMUM of the two side-band
    means (not a linear side-band interpolation) — a robust ROI netting that avoids a neighbouring
    photopeak inflating the baseline, at the cost of being biased low on a sloped continuum."""
    m = window_mask(E)
    lo, hi, bw = E * (1 - WF), E * (1 + WF), 0.05 * E
    left = s[(ctr > lo - bw) & (ctr < lo)]
    right = s[(ctr > hi) & (ctr < hi + bw)]
    # min of the two side bands: the underlying continuum comes from below, and using the
    # lower side avoids a neighbouring photopeak (e.g. the close Co-60 1173/1332 doublet)
    # inflating the baseline.
    lvl = [b.mean() for b in (left, right) if len(b)]
    base = min(lvl) if lvl else 0.0
    return max(s[m].sum() - base * m.sum(), 0.0)

# --- raw window quantification (gross counts in each photopeak window, the naive LLD/ULD count) ---
raw = {(n, E): spec[window_mask(E)].sum() for n, E, _ in LINES}

# --- Compton stripping, top-down ---
strip = spec.copy()
stripped = {}
model_continua = {}
for name, E, _ in LINES:                      # LINES already sorted high -> low
    P_est = net_area(strip, E)                # photopeak sits above its own Compton edge -> clean
    stripped[(name, E)] = P_est
    pf = photofraction(E)
    C = P_est * (1.0 - pf) / pf                # continuum counts to remove
    shape = continuum_shape(E)
    model_continua[(name, E)] = C * shape
    strip = np.clip(strip - C * shape, 0.0, None)

# fair truth: the photopeak fraction actually inside the +-WF window (resolution vs width)
def inwindow_truth(E, P):
    frac = math.erf(WF * E / (math.sqrt(2.0) * gauss_res_sigma(E)))
    return P * frac

inwin = {(n, E): inwindow_truth(E, P) for n, E, P in LINES}

print(f"{'line':16s} {'in-win true':>11s} {'raw window':>11s} {'stripped':>9s}  raw err / stripped err")
for name, E, _ in LINES:
    t = inwin[(name, E)]
    r = raw[(name, E)]
    s = stripped[(name, E)]
    print(f"{name} {E:6.1f}   {t:11.0f} {r:11.0f} {s:9.0f}   {(r-t)/t*100:+5.0f}% / {(s-t)/t*100:+5.0f}%")

# --- plot ---
fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(15, 5.2))

ax1.semilogy(ctr, np.clip(spec, 0.5, None), color="#c0392b", lw=1.2, label="measured spectrum")
total_model = sum(model_continua.values())
ax1.semilogy(ctr, np.clip(total_model, 0.5, None), color="#e67e22", lw=1.2, ls="--",
             label="modeled Compton continua (all lines)")
ax1.semilogy(ctr, np.clip(strip, 0.5, None), color="#3b6ea5", lw=1.4, label="after stripping")
for name, E, _ in LINES:
    ax1.axvline(E, color="gray", ls=":", lw=0.7)
    ax1.text(E + 6, ax1.get_ylim()[1] * 0.5, f"{name}\n{E:.0f}", fontsize=7, rotation=90, va="top")
ax1.axvspan(661.7 * (1 - WF), 661.7 * (1 + WF), color="#4dd0e1", alpha=0.18)
ax1.set_xlabel("energy (keV)"); ax1.set_ylabel("counts (log)")
ax1.set_title("Compton stripping: subtract each line's modeled continuum top-down\n"
              "(the Cs-137 662 window sits under Co-60's continua)")
ax1.legend(fontsize=8); ax1.set_ylim(0.5, None)

names = [f"{n}\n{E:.0f}" for n, E, _ in LINES]
x = np.arange(len(LINES)); w = 0.27
tvals = [inwin[(n, E)] for n, E, _ in LINES]
rvals = [raw[(n, E)] for n, E, _ in LINES]
svals = [stripped[(n, E)] for n, E, _ in LINES]
ax2.bar(x - w, tvals, w, label="in-window true net", color="#2ecc71")
ax2.bar(x, rvals, w, label="raw window (contaminated)", color="#c0392b")
ax2.bar(x + w, svals, w, label="stripped", color="#3b6ea5")
for i, (n, E, _) in enumerate(LINES):
    ax2.text(i, rvals[i], f"{(rvals[i]-tvals[i])/tvals[i]*100:+.0f}%", ha="center", va="bottom",
             fontsize=8, color="#c0392b")
    ax2.text(i + w, svals[i], f"{(svals[i]-tvals[i])/tvals[i]*100:+.0f}%", ha="center", va="bottom",
             fontsize=8, color="#3b6ea5")
ax2.set_xticks(x); ax2.set_xticklabels(names, fontsize=8)
ax2.set_ylabel("net photopeak counts")
ax2.set_title("Stripping recovers well-separated lines (Cs 662: +39%->-5%);\n"
              "error accumulates top-down (Co-60 doublet leak -> Co-57 over-corrected)")
ax2.legend(fontsize=8)
fig.tight_layout()
fig.savefig("compton_stripping.png", dpi=130)
print("saved compton_stripping.png")
