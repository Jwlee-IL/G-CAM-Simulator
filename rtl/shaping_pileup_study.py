"""Phase A2: the fix -- gated charge-integration + pile-up REJECTION restores trustworthy
energy, and the shaping (integration-window) length trades resolution against throughput.

Compares, on identical waveforms, two RTL front-ends:
  * baseline  peak_detector.sv          -- measures every pulse's peak, emits it blindly
                                           (a simple peak detector -> contaminated at rate)
  * fixed     integrating_peak_detector.sv -- integrates charge over `window`, and REJECTS
                                           events where a 2nd pulse lands in the window or the
                                           signal hasn't returned to baseline (tail pile-up)

Three results:
  1. Spectrum cleanup at a fixed high rate: rejection collapses the fake pile-up sum-tail.
  2. Trust vs rate: fake-sum fraction (>720 keV), baseline vs reject-accepted.
  3. The tradeoff: integration window (shaping time) vs resolution & throughput, fast vs slow
     crystal -> longer window = better resolution but more dead time (lower live fraction),
     and a fast crystal keeps throughput up.

Run from rtl/ with iverilog/vvp reachable:  python shaping_pileup_study.py
"""
import os
import csv
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
ADC_PER_KEV = 3000.0 / 662.0
ADC_MAX = 4095
COMPTON_EDGE = 477.0

subprocess.run(["iverilog", "-g2012", "-o", "sim.vvp",
                "tb_peak_detector.sv", "peak_detector.sv"], check=True)
subprocess.run(["iverilog", "-g2012", "-o", "sim_int.vvp",
                "tb_integrating.sv", "integrating_peak_detector.sv"], check=True)


def shape_for(decay_ns):
    tau_d = max(1.0, decay_ns / 10.0)
    tau_r = max(0.5, tau_d / 5.0)
    L = int(max(64, 8 * tau_d))
    n = np.arange(L)
    s = np.exp(-n / tau_d) - np.exp(-n / tau_r)
    s /= s.max()
    return s, L


def gen(rate, decay_ns, n_events=1200, seed=4242):
    shape, L = shape_for(decay_ns)
    rng = np.random.default_rng(seed)
    gaps = rng.exponential(FS / rate, n_events).astype(int) + 1
    times = np.cumsum(gaps) + 400
    total = int(times[-1] + L + 400)
    E = np.empty(n_events)
    pp = rng.random(n_events) < 0.6
    E[pp] = rng.normal(662.0, 0.07 * 662 / 2.355, pp.sum())
    E[~pp] = rng.uniform(30.0, COMPTON_EDGE, (~pp).sum())
    E = np.clip(E, 0.0, None)
    wave = np.full(total, float(BASELINE))
    for t, e in zip(times, E):
        end = min(t + L, total)
        wave[t:end] += e * ADC_PER_KEV * shape[:end - t]
    wave += rng.normal(0.0, NOISE, total)
    return np.clip(np.round(wave), 0, ADC_MAX).astype(int), E


def run_peakhold(adc):
    np.savetxt("adc.txt", adc, fmt="%d")
    subprocess.run(["vvp", "sim.vvp"], check=True, capture_output=True)
    if os.path.getsize("peaks.txt") == 0:
        return np.array([])
    det = np.loadtxt("peaks.txt")
    det = det.reshape(-1, 2) if det.ndim == 1 else det
    return det[:, 1] / ADC_PER_KEV          # keV, ALL events (no rejection)


def run_integrating(adc, window):
    np.savetxt("adc.txt", adc, fmt="%d")
    subprocess.run(["vvp", "sim_int.vvp", f"+WINDOW={window}"], check=True, capture_output=True)
    if os.path.getsize("peaks_int.txt") == 0:
        return np.zeros((0, 3))
    det = np.loadtxt("peaks_int.txt")
    return det.reshape(-1, 3) if det.ndim == 1 else det   # (time, energy_int, reject)


def calib_int(window, decay_ns):
    """Integrated charge of an isolated 662 keV pulse for this window (energy scale)."""
    shape, L = shape_for(decay_ns)
    wave = np.full(500 + L, float(BASELINE))
    wave[300:300 + L] += 662.0 * ADC_PER_KEV * shape
    det = run_integrating(np.clip(np.round(wave), 0, ADC_MAX).astype(int), window)
    good = det[det[:, 2] == 0]
    return good[:, 1].max() if len(good) else np.nan


def fwhm_of(keV):
    near = keV[(keV > 600) & (keV < 720)]
    return 2.355 * near.std() / near.mean() * 100 if len(near) > 5 else np.nan


# ---------------------------------------------------------------------------
DECAY_MAIN = 55.0    # GAGG:Mg
WIN_MAIN = 50        # nominal integration window (samples = 500 ns)
cal = calib_int(WIN_MAIN, DECAY_MAIN)
print(f"calibration: isolated 662 keV -> integrated {cal:.0f} (window {WIN_MAIN})")

# 1. Spectrum cleanup at a fixed high rate --------------------------------
RATE_HI = 1e6
adc, trueE = gen(RATE_HI, DECAY_MAIN)
ph_keV = run_peakhold(adc)
di = run_integrating(adc, WIN_MAIN)
int_keV = di[:, 1] / cal * 662.0
acc = int_keV[di[:, 2] == 0]        # accepted (reject=0)
rej = int_keV[di[:, 2] == 1]        # rejected
print(f"[{RATE_HI/1e6:.0f} Mcps] peak-hold events={len(ph_keV)} "
      f"sum-tail>720={np.mean(ph_keV>720)*100:.1f}%  |  "
      f"integrating accepted={len(acc)} rejected={len(rej)} "
      f"accepted sum-tail>720={np.mean(acc>720)*100:.1f}%")

# 2. Trust vs rate ---------------------------------------------------------
RATES = [50e3, 100e3, 200e3, 500e3, 1e6, 2e6]
rate_rows = []
for r in RATES:
    a, _ = gen(r, DECAY_MAIN)
    pk = run_peakhold(a)
    d = run_integrating(a, WIN_MAIN)
    ik = d[:, 1] / cal * 662.0
    ak = ik[d[:, 2] == 0]
    tail_pk = np.mean(pk > 720) * 100 if len(pk) else np.nan
    tail_naive = np.mean(ik > 720) * 100 if len(ik) else np.nan   # integrate, ignore reject flag
    tail_ac = np.mean(ak > 720) * 100 if len(ak) else np.nan
    cen_pk = pk[(pk > 600) & (pk < 720)].mean() if len(pk) else np.nan
    cen_ac = ak[(ak > 600) & (ak < 720)].mean() if len(ak) else np.nan
    rate_rows.append((r, tail_pk, tail_ac, tail_naive, cen_pk, cen_ac))
    print(f"  {r/1e3:6.0f}k/s  peak-hold sum-tail={tail_pk:4.1f}%  naive-integrate={tail_naive:4.1f}%  "
          f"reject={tail_ac:4.1f}%  (cen {cen_pk:6.1f} -> {cen_ac:6.1f})")

# 3. Window (shaping) sweep: resolution vs throughput, fast vs slow crystal --
WINDOWS = [20, 30, 50, 80, 120]
RATE_TR = 500e3
tradeoff = {}
for label, decay in [("CeBr3 (17 ns)", 17.0), ("GAGG (90 ns)", 90.0)]:
    adc_tr, tE = gen(RATE_TR, decay)
    n_true = len(tE)
    pts = []
    for w in WINDOWS:
        c = calib_int(w, decay)
        d = run_integrating(adc_tr, w)
        ik = d[:, 1] / c * 662.0
        ak = ik[d[:, 2] == 0]
        live = len(ak) / n_true * 100        # throughput: accepted / true
        res = fwhm_of(ak)
        pts.append((w, res, live))
        print(f"  {label:14s} win={w:3d} ({w*10}ns)  FWHM={res:4.1f}%  throughput={live:4.1f}%")
    tradeoff[label] = pts

# --- CSV ---
with open("shaping_pileup.csv", "w", newline="") as f:
    w = csv.writer(f)
    w.writerow(["section", "x", "series", "value"])
    for r, tpk, tac, tnaive, cpk, cac in rate_rows:
        w.writerow(["rate", f"{r:.0f}", "peakhold_sumtail", f"{tpk:.2f}"])
        w.writerow(["rate", f"{r:.0f}", "naive_integrate_sumtail", f"{tnaive:.2f}"])
        w.writerow(["rate", f"{r:.0f}", "reject_sumtail", f"{tac:.2f}"])
        w.writerow(["rate", f"{r:.0f}", "peakhold_centroid", f"{cpk:.1f}"])
        w.writerow(["rate", f"{r:.0f}", "reject_centroid", f"{cac:.1f}"])
    for label, pts in tradeoff.items():
        for wv, res, live in pts:
            w.writerow([f"tradeoff:{label}", f"{wv}", "fwhm", f"{res:.2f}"])
            w.writerow([f"tradeoff:{label}", f"{wv}", "throughput", f"{live:.2f}"])

# --- plots ---
fig, axes = plt.subplots(1, 3, figsize=(16, 4.8))

ax = axes[0]
bins = np.linspace(0, 1000, 90)
ax.hist(ph_keV, bins=bins, histtype="step", color="#c0392b", lw=1.6,
        label=f"peak-hold, all ({len(ph_keV)}) -- sum-tail {np.mean(ph_keV>720)*100:.0f}%")
ax.hist(acc, bins=bins, histtype="stepfilled", color="#3b6ea5", alpha=0.55,
        label=f"integrate+reject accepted ({len(acc)})")
ax.axvline(662, ls=":", color="k", lw=1); ax.axvspan(720, 1000, color="#c0392b", alpha=0.06)
ax.text(830, ax.get_ylim()[1]*0.7, "fake\nsum\nregion", ha="center", fontsize=8, color="#c0392b")
ax.set_xlabel("energy (keV)"); ax.set_ylabel("counts")
ax.set_title(f"1. Spectrum @ {RATE_HI/1e6:.0f} Mcps: rejection cuts the fake sums")
ax.legend(fontsize=7.5)

ax = axes[1]
rr = np.array([r[0] for r in rate_rows]) / 1e3
ax.semilogx(rr, [r[3] for r in rate_rows], "^--", color="#e67e22", lw=1.6, label="naive integrate (no reject)")
ax.semilogx(rr, [r[1] for r in rate_rows], "s-", color="#c0392b", lw=2, label="peak-hold (measure all)")
ax.semilogx(rr, [r[2] for r in rate_rows], "o-", color="#3b6ea5", lw=2, label="integrate + reject")
ax.set_xlabel("count rate (kcps)"); ax.set_ylabel("fake sum-tail >720 keV (%)")
ax.set_title("2. Trust vs rate: naive integrate is worst;\nrejection roughly halves the fake tail")
ax.legend(fontsize=8); ax.grid(alpha=0.3, which="both")

ax = axes[2]
colors = {"CeBr3 (17 ns)": "#4C9F70", "GAGG (90 ns)": "#C0504D"}
for label, pts in tradeoff.items():
    res = [p[1] for p in pts]; live = [p[2] for p in pts]
    ax.plot(res, live, "o-", color=colors[label], lw=2, label=label)
    for (wv, r, lv) in pts:
        ax.annotate(f"{wv*10}ns", (r, lv), fontsize=6.5, color=colors[label],
                    textcoords="offset points", xytext=(3, 3))
ax.set_xlabel("photopeak FWHM (%)  <- better")
ax.set_ylabel("throughput: accepted / true (%)  better ->")
ax.set_title(f"3. Shaping tradeoff @ {RATE_TR/1e3:.0f} kcps\n(longer window: better res, lower throughput)")
ax.legend(fontsize=8); ax.grid(alpha=0.3)

fig.suptitle("Phase A2 -- charge integration + pile-up rejection: trustworthy energy at the cost of throughput",
             fontsize=13)
fig.tight_layout(rect=[0, 0, 1, 0.95])
fig.savefig("shaping_pileup.png", dpi=130)
print("saved shaping_pileup.png, shaping_pileup.csv")
