"""Phase B: multi-isotope discrimination + ADC dynamic range.

A real field is not one clean line. This drives the A2 charge-integrating + pile-up-rejecting
front-end (integrating_peak_detector.sv) with a Cs-137 + Co-60 + Co-57 field -- each line
carrying its Klein-Nishina Compton continuum -- and reconstructs the energy spectrum at two
ADC gains:

  * Cs-137 gain (662 keV -> 3000 counts): 662 sits nicely, but usable headroom is only ~859 keV, so
    Co-60 (1173/1332) SATURATES into a false edge -- unmeasurable. This is the real system's mistake:
    tuning the gain to the primary source.
  * low gain (1332 keV under full scale): Co-60 1173/1332 resolve, AND the 122 keV line is unhurt
    (its width is set by intrinsic ~1/sqrt(E) statistics, not the gain; ADC quantization/noise are
    negligible at 12-bit). So a SINGLE gain set to the highest line covers the whole 122->1332 keV
    span -- dual-gain/companding is only needed with fewer ADC bits or a large fixed noise floor.
  (The Co-57 122 keV line sits on the Compton continua of the higher lines -- raised background,
  not full burial, at comparable activities.)

Run from rtl/ with iverilog/vvp reachable:  python multi_isotope_study.py
"""
import os
import json
import glob
import subprocess
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

os.chdir(os.path.dirname(os.path.abspath(__file__)))
os.environ["PATH"] = os.path.expanduser("~/scoop/shims") + os.pathsep + os.environ["PATH"]

FS = 100e6
BASELINE = 200
NOISE = 3.0
ADC_MAX = 4095
DECAY_NS = 55.0          # GAGG:Mg representative crystal
WINDOW = 50
THRESH_COUNTS = 80       # matches the RTL trigger threshold

subprocess.run(["iverilog", "-g2012", "-o", "sim_int.vvp",
                "tb_integrating.sv", "integrating_peak_detector.sv"], check=True)

# --- isotope presets (data model: multi-line + cascade flag) ---
ISOTOPES = {}
for path in sorted(glob.glob("../samples/isotopes/*.json")):
    d = json.load(open(path))
    ISOTOPES[d["name"]] = d

# photopeak fraction (full-energy absorption prob) vs energy -- approximate for ~10 mm GAGG
def photofraction(E):
    return float(np.clip(0.93 * (150.0 / E) ** 0.55, 0.12, 0.95))


def shape_for(decay_ns):
    tau_d = max(1.0, decay_ns / 10.0)
    tau_r = max(0.5, tau_d / 5.0)
    L = int(max(64, 8 * tau_d))
    n = np.arange(L)
    s = np.exp(-n / tau_d) - np.exp(-n / tau_r)
    s /= s.max()
    return s, L


def compton_deposit(E, n, rng):
    """Sample deposited (electron) energy from the Klein-Nishina Compton continuum, 0..edge."""
    a = E / 511.0
    Emin = E / (1.0 + 2.0 * a)                    # backscattered photon energy
    Ep = np.linspace(Emin, E, 400)
    cos = 1.0 - 511.0 * (1.0 / Ep - 1.0 / E)      # Compton relation
    sin2 = np.clip(1.0 - cos * cos, 0.0, 1.0)
    w = np.clip(Ep / E + E / Ep - sin2, 0.0, None)  # Klein-Nishina angular factor
    cdf = np.cumsum(w); cdf /= cdf[-1]
    Eprime = np.interp(rng.random(n), cdf, Ep)
    return E - Eprime                              # electron energy = deposit


P_BOTH = 0.08   # prob BOTH cascade gammas are detected (~ geometric eff^2): the sum peak is
                # rate-linear but eps^2-suppressed vs the singles, so single lines dominate.

def build_field(activities, seconds, rng):
    """Return sorted (time_sample, deposit_keV) for a multi-isotope field. A cascade decay
    usually deposits just ONE of its gammas (small detector); occasionally BOTH, coincident
    (they then sum in the waveform) -> a small rate-linear cascade-sum peak."""
    gammas_t, gammas_E = [], []
    for name, act in activities.items():
        iso = ISOTOPES[name]
        n_dec = int(act * seconds)
        t = np.cumsum(rng.exponential(FS / act, n_dec)).astype(int) + 400
        lines = iso["lines"]
        br = np.array([ln["branching"] for ln in lines])
        if iso["cascadeCoincident"] and len(lines) > 1:
            both = rng.random(n_dec) < P_BOTH
            # both-detected decays: every line, coincident (same timestamp -> sum)
            for ln in lines:
                gammas_t.append(t[both]); gammas_E.append(np.full(both.sum(), ln["keV"]))
            # single-detected decays: one line by relative branching
            sidx = np.where(~both)[0]
            pick = rng.choice(len(lines), size=sidx.size, p=br / br.sum())
            gammas_t.append(t[sidx]); gammas_E.append(np.array([lines[i]["keV"] for i in pick]))
        else:
            # one line per decay, chosen by relative branching (mutually exclusive paths)
            emit = rng.random(n_dec) < br.sum()
            pick = rng.choice(len(lines), size=emit.sum(), p=br / br.sum())
            gammas_t.append(t[emit]); gammas_E.append(np.array([lines[i]["keV"] for i in pick]))
    tt = np.concatenate(gammas_t); EE = np.concatenate(gammas_E)
    # each gamma deposits: photopeak (full E) or a Compton-continuum energy
    dep = EE.copy()
    for E in np.unique(EE):
        m = EE == E
        n = int(m.sum())
        pp = rng.random(n) < photofraction(E)
        d = np.empty(n)
        d[pp] = E
        d[~pp] = compton_deposit(E, (~pp).sum(), rng)
        dep[m] = d
    # energy resolution smear (statistical ~1/sqrt(E))
    sigma = 0.07 * 662.0 / 2.355 * np.sqrt(np.clip(dep, 1, None) / 662.0)
    dep = np.clip(dep + rng.normal(0, sigma), 0, None)
    order = np.argsort(tt)
    return tt[order], dep[order]


def waveform(times, dep_keV, gain, shape, L):
    total = int(times[-1] + L + 400)
    wave = np.full(total, float(BASELINE))
    amp = dep_keV * gain
    for t, a in zip(times, amp):
        end = min(t + L, total)
        wave[t:end] += a * shape[:end - t]
    wave += np.random.default_rng(9).normal(0, NOISE, total)
    return np.clip(np.round(wave), 0, ADC_MAX).astype(int)


def run_integrating(adc, window=WINDOW):
    np.savetxt("adc.txt", adc, fmt="%d")
    subprocess.run(["vvp", "sim_int.vvp", f"+WINDOW={window}"], check=True, capture_output=True)
    if os.path.getsize("peaks_int.txt") == 0:
        return np.zeros((0, 3))
    det = np.loadtxt("peaks_int.txt")
    return det.reshape(-1, 3) if det.ndim == 1 else det


def calib(gain, shape, L):
    wave = np.full(500 + L, float(BASELINE))
    wave[300:300 + L] += 662.0 * gain * shape
    d = run_integrating(np.clip(np.round(wave), 0, ADC_MAX).astype(int))
    good = d[d[:, 2] == 0]
    return good[:, 1].max() if len(good) else np.nan


# --- field: comparable line rates across the three isotopes ---
shape, L = shape_for(DECAY_NS)
rng = np.random.default_rng(2027)
ACTIV = {"Cs-137": 80e3, "Co-60": 60e3, "Co-57": 90e3}   # decays/s
SECONDS = 0.02
times, dep = build_field(ACTIV, SECONDS, rng)
print(f"field: {len(times)} gamma deposits, "
      f"{', '.join(f'{k} {v/1e3:.0f}k/s' for k,v in ACTIV.items())}")

GAINS = {
    "Cs-137 gain (662->3000)": 3000.0 / 662.0,
    "low gain (1332 under FS)": 3600.0 / 1332.0,
}

fig, axes = plt.subplots(1, 2, figsize=(15, 5.2), sharey=True)
for ax, (label, gain) in zip(axes, GAINS.items()):
    fs_keV = (ADC_MAX - BASELINE) / gain
    thr_keV = THRESH_COUNTS / gain
    cal = calib(gain, shape, L)
    adc = waveform(times, dep, gain, shape, L)
    d = run_integrating(adc)
    keV = d[:, 1] / cal * 662.0
    acc = keV[d[:, 2] == 0]
    bins = np.linspace(0, 1600, 200)
    ax.hist(acc, bins=bins, histtype="stepfilled", color="#3b6ea5", alpha=0.65, log=True)
    for E, name, col in [(122, "Co-57 122", "#4C9F70"), (662, "Cs-137 662", "#000000"),
                         (1173, "Co-60 1173", "#C0504D"), (1332, "Co-60 1332", "#C0504D")]:
        if E < fs_keV:
            ax.axvline(E, ls=":", color=col, lw=1)
            ax.text(E + 8, ax.get_ylim()[1] * 0.35, name, rotation=90, fontsize=7, color=col, va="top")
    ax.axvline(fs_keV, color="#e67e22", ls="--", lw=1.5)
    ax.text(fs_keV - 12, ax.get_ylim()[1] * 0.6, f"full scale {fs_keV:.0f} keV",
            rotation=90, fontsize=8, color="#e67e22", ha="right", va="top")
    ax.axvspan(0, thr_keV, color="gray", alpha=0.15)
    ax.text(thr_keV + 5, 1.5, f"thresh {thr_keV:.0f} keV", fontsize=7, color="gray")
    if fs_keV < 1173:
        ax.axvspan(fs_keV, 1600, color="#C0504D", alpha=0.07)
        ax.text((fs_keV + 1600) / 2, ax.get_ylim()[1] * 0.5,
                "Co-60\nSATURATES\n(off-scale ->\npiles at FS)", ha="center", fontsize=8, color="#C0504D")
    verdict = "the mistake: Co-60 clips" if fs_keV < 1332 else "Co-60 1173/1332 resolved, 122 keV unhurt"
    ax.set_xlabel("reconstructed energy (keV)")
    ax.set_title(f"{label}  -- {verdict}\naccepted {len(acc)} events")
axes[0].set_ylabel("counts (log)")
fig.suptitle("Phase B -- multi-isotope field (Cs-137 + Co-60 + Co-57): set the ADC gain to the HIGHEST line, not the primary source",
             fontsize=13)
fig.tight_layout(rect=[0, 0, 1, 0.95])
fig.savefig("multi_isotope.png", dpi=130)
print("saved multi_isotope.png")

# ===========================================================================
# (iii) cascade vs pile-up sum: rate scaling separates them
#   A Co-60 decay emits its two gammas simultaneously -> a TRUE-coincidence (cascade) sum is
#   rate-LINEAR (one per decay, prob P_BOTH). Two gammas from DIFFERENT decays landing in the
#   same window is a random PILE-UP sum, rate-QUADRATIC. On the FPGA both look like one fake
#   high-energy event; here, sweeping activity separates them by their slope (1 vs 2) and shows
#   the crossover activity above which the "sum peak" is mostly artifact.
print("(iii) cascade vs pile-up sum, Co-60 activity sweep")
LEN_S = 0.008
GAIN_III = 3600.0 / 1332.0
ACTS = [50e3, 100e3, 200e3, 400e3, 800e3, 1.6e6]
casc_rate, pile_rate = [], []
for A in ACTS:
    r3 = np.random.default_rng(int(A) ^ 0x5151)
    t3, d3 = build_field({"Co-60": A}, LEN_S, r3)
    adc3 = waveform(t3, d3, GAIN_III, shape, L)
    ev = run_integrating(adc3)
    ncas = npil = 0
    for trig in ev[:, 0].astype(int):
        m = (t3 >= trig - 2) & (t3 < trig + WINDOW)
        if m.sum() >= 2:
            tw = t3[m]
            if np.unique(tw).size < tw.size:   # a duplicate timestamp => same decay => cascade
                ncas += 1
            else:
                npil += 1
    casc_rate.append(ncas / LEN_S)
    pile_rate.append(npil / LEN_S)
    print(f"  A={A/1e3:6.0f}k/s  cascade-sum={ncas/LEN_S:8.0f}/s  random-pileup={npil/LEN_S:8.0f}/s")

fig2, ax = plt.subplots(figsize=(7.5, 5.5))
A_arr = np.array(ACTS) / 1e3
ax.loglog(A_arr, casc_rate, "o-", color="#3b6ea5", lw=2, label="cascade sum (same decay) -- slope 1")
ax.loglog(A_arr, pile_rate, "s-", color="#c0392b", lw=2, label="random pile-up (2 decays) -- slope 2")
# reference slopes anchored at the first point
ax.loglog(A_arr, casc_rate[0] * (A_arr / A_arr[0]), ":", color="#3b6ea5", lw=1, alpha=0.6)
ax.loglog(A_arr, max(pile_rate[0], 1) * (A_arr / A_arr[0]) ** 2, ":", color="#c0392b", lw=1, alpha=0.6)
ax.set_xlabel("Co-60 activity (kdecays/s)"); ax.set_ylabel("sum-event rate (/s)")
ax.set_title("(iii) Cascade vs pile-up sum: rate scaling tells them apart\n"
             "(linear cascade floor vs quadratic pile-up -> crossover = 'is the sum peak real?')")
ax.legend(fontsize=9); ax.grid(alpha=0.3, which="both")
fig2.tight_layout()
fig2.savefig("cascade_pileup.png", dpi=130)
print("saved cascade_pileup.png")

# quick numeric readout (incl. the 122 keV low-line resolution penalty)
def peak_fwhm(keV, center, half=18):
    w = keV[(keV > center - half) & (keV < center + half)]
    return 2.355 * w.std() / w.mean() * 100 if len(w) > 8 else float("nan")

for label, gain in GAINS.items():
    fs = (ADC_MAX - BASELINE) / gain
    cal = calib(gain, shape, L)
    d = run_integrating(waveform(times, dep, gain, shape, L))
    keV = (d[d[:, 2] == 0][:, 1] / cal * 662.0)
    sat = np.mean(keV > fs - 30) * 100
    co60_ok = "yes" if 1332 < fs else "NO (saturates)"
    print(f"  {label:26s} FS={fs:5.0f}keV  near-FS(sat) {sat:4.1f}%  "
          f"122keV FWHM={peak_fwhm(keV,122):4.1f}%  662 FWHM={peak_fwhm(keV,662):4.1f}%  "
          f"Co-60 measurable={co60_ok}")
