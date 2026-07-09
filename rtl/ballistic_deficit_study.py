"""Phase A: why the simple peak-hold front-end cannot be trusted on energy.

Drives peak_detector.sv (unchanged) with three probes and quantifies where its
reconstructed energy goes wrong -- the groundwork before multi-isotope discrimination:

  1. Isolated-pulse fidelity + dynamic range.
     A pure peak-hold captures the true maximum, so an ISOLATED pulse has *no*
     ballistic deficit -- measured == true, up to full scale. But with the gain set
     so Cs-137 662 keV = 3000 counts, usable headroom (4095-BASELINE) is only ~859 keV, so Co-60
     (1173/1333 keV) SATURATES: the multi-isotope field squeezes the ADC dynamic range.

  2. Two-pulse overlap (the "ballistic deficit" that actually bit the FPGA).
     Two pulses separated by dt: when they overlap the peak-hold merges them into one
     detection whose amplitude is the summed max -> a FALSE higher-energy line, and it
     misses a count. A slow crystal (long tail) stays merged for a longer dt, so its
     resolving time is worse. Sweep dt for a fast (CeBr3) and a slow (GAGG) crystal.

  3. Count-rate sweep: energy trust vs rate.
     As the rate climbs, pile-up merges shift the photopeak centroid and grow a tail of
     fake high-energy (sum) events above the 662 keV line.

Run from rtl/ with iverilog/vvp reachable:  python ballistic_deficit_study.py
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

FS = 100e6              # 100 MHz (10 ns/sample) -- matches the testbench clock
BASELINE = 200
NOISE = 3.0
ADC_PER_KEV = 3000.0 / 662.0   # gain: Cs-137 photopeak -> 3000 counts
ADC_MAX = 4095                 # 12-bit ADC full scale
FULLSCALE_KEV = (ADC_MAX - BASELINE) / ADC_PER_KEV  # ~= 859 keV of headroom above baseline

subprocess.run(["iverilog", "-g2012", "-o", "sim.vvp",
                "tb_peak_detector.sv", "peak_detector.sv"], check=True)


def shape_for(decay_ns):
    """Bi-exponential scintillation pulse (finite rise = charge collection), peak=1."""
    tau_d = max(1.0, decay_ns / 10.0)
    tau_r = max(0.5, tau_d / 5.0)
    L = int(max(64, 8 * tau_d))
    n = np.arange(L)
    s = np.exp(-n / tau_d) - np.exp(-n / tau_r)
    s /= s.max()
    return s, int(np.argmax(s))


def run_rtl(wave):
    """Feed one waveform through the RTL peak detector, return detected (time, amp) rows."""
    adc = np.clip(np.round(wave), 0, ADC_MAX).astype(int)
    np.savetxt("adc.txt", adc, fmt="%d")
    subprocess.run(["vvp", "sim.vvp"], check=True, capture_output=True)
    if os.path.getsize("peaks.txt") == 0:
        return np.zeros((0, 2))
    det = np.loadtxt("peaks.txt")
    return det.reshape(-1, 2) if det.ndim == 1 else det


# ---------------------------------------------------------------------------
# Probe 1: isolated-pulse fidelity + dynamic range (clip above full scale)
# ---------------------------------------------------------------------------
def probe_isolated():
    shape, pk = shape_for(55.0)  # GAGG:Mg representative; isolated -> shape-independent
    L = len(shape)
    true_keV = np.array([59, 122, 356, 477, 662, 834, 1173, 1332], float)
    meas = []
    for e in true_keV:
        m = []
        for seed in range(8):
            rng = np.random.default_rng(1000 + seed)
            wave = np.full(600 + L, float(BASELINE))
            wave[450:450 + L] += e * ADC_PER_KEV * shape
            wave += rng.normal(0.0, NOISE, wave.size)
            det = run_rtl(wave)
            if len(det):
                m.append(det[:, 1].max() / ADC_PER_KEV)
        meas.append(np.mean(m) if m else np.nan)
    return true_keV, np.array(meas)


# ---------------------------------------------------------------------------
# Probe 2: two equal pulses vs separation dt -> merge (fake sum line) + resolving time
# ---------------------------------------------------------------------------
def probe_overlap(decay_ns, e_keV=300.0, seeds=6):
    shape, pk = shape_for(decay_ns)
    L = len(shape)
    dts = np.arange(4, 80, 4)          # separation in samples (40 ns .. 800 ns)
    n_res, meas_max = [], []
    for dt in dts:
        nres, mx = [], []
        for seed in range(seeds):
            rng = np.random.default_rng(2000 + seed)
            total = 450 + dt + L + 200
            wave = np.full(total, float(BASELINE))
            wave[450:450 + L] += e_keV * ADC_PER_KEV * shape
            b = 450 + dt
            wave[b:b + L] += e_keV * ADC_PER_KEV * shape
            wave += rng.normal(0.0, NOISE, total)
            det = run_rtl(wave)
            nres.append(len(det))
            mx.append(det[:, 1].max() / ADC_PER_KEV if len(det) else np.nan)
        n_res.append(np.mean(nres))
        meas_max.append(np.nanmean(mx))
    return dts * (1e9 / FS), np.array(n_res), np.array(meas_max)  # dt in ns


# ---------------------------------------------------------------------------
# Probe 3: rate sweep -> photopeak centroid drift + high-energy sum-tail fraction
# ---------------------------------------------------------------------------
def probe_rate(decay_ns=55.0, n_events=1200):
    shape, pk = shape_for(decay_ns)
    L = len(shape)
    rates = [50e3, 100e3, 200e3, 500e3, 1e6, 2e6]
    out = []
    spectra = {}
    for rate in rates:
        rng = np.random.default_rng(3333)
        gaps = rng.exponential(FS / rate, n_events).astype(int) + 1
        times = np.cumsum(gaps) + 400
        total = int(times[-1] + L + 400)
        E = np.empty(n_events)
        pp = rng.random(n_events) < 0.6
        E[pp] = rng.normal(662.0, 0.07 * 662 / 2.355, pp.sum())
        E[~pp] = rng.uniform(30.0, 477.0, (~pp).sum())
        E = np.clip(E, 0.0, None)
        wave = np.full(total, float(BASELINE))
        for t, e in zip(times, E):
            end = min(t + L, total)
            wave[t:end] += e * ADC_PER_KEV * shape[:end - t]
        wave += rng.normal(0.0, NOISE, total)
        det = run_rtl(wave)
        keV = det[:, 1] / ADC_PER_KEV if len(det) else np.array([])
        near = keV[(keV > 600) & (keV < 720)]
        centroid = near.mean() if len(near) > 5 else np.nan
        fwhm = 2.355 * near.std() / near.mean() * 100 if len(near) > 5 else np.nan
        sum_tail = float(np.mean(keV > 720)) if len(keV) else np.nan  # fake high-E fraction
        out.append((rate, centroid, fwhm, sum_tail))
        spectra[rate] = keV
        print(f"  {rate/1e3:6.0f}k/s  centroid={centroid:6.1f}keV  FWHM={fwhm:4.1f}%  "
              f"sum-tail(>720)={sum_tail*100:4.1f}%")
    return out, spectra


# ===========================================================================
print("probe 1: isolated-pulse fidelity + dynamic range")
tk, mk = probe_isolated()
for e, m in zip(tk, mk):
    flag = "  <-- SATURATED" if e > FULLSCALE_KEV else ""
    print(f"  true {e:6.0f} keV -> measured {m:6.1f} keV{flag}")

print("probe 2: two-pulse overlap (resolving time / merge)")
dt_ns_f, nres_f, mx_f = probe_overlap(17.0)   # CeBr3 fast
dt_ns_s, nres_s, mx_s = probe_overlap(90.0)   # GAGG slow

print("probe 3: rate sweep energy trust")
rate_rows, spectra = probe_rate()

# --- write CSV ---
with open("ballistic_deficit.csv", "w", newline="") as f:
    w = csv.writer(f)
    w.writerow(["probe", "x", "series", "value"])
    for e, m in zip(tk, mk):
        w.writerow(["isolated", f"{e:.0f}", "measured_keV", f"{m:.2f}"])
    for dt, nr, mx in zip(dt_ns_f, nres_f, mx_f):
        w.writerow(["overlap_CeBr3", f"{dt:.0f}", "n_detected", f"{nr:.2f}"])
        w.writerow(["overlap_CeBr3", f"{dt:.0f}", "meas_max_keV", f"{mx:.1f}"])
    for dt, nr, mx in zip(dt_ns_s, nres_s, mx_s):
        w.writerow(["overlap_GAGG", f"{dt:.0f}", "n_detected", f"{nr:.2f}"])
        w.writerow(["overlap_GAGG", f"{dt:.0f}", "meas_max_keV", f"{mx:.1f}"])
    for rate, cen, fw, st in rate_rows:
        w.writerow(["rate", f"{rate:.0f}", "centroid_keV", f"{cen:.1f}"])
        w.writerow(["rate", f"{rate:.0f}", "fwhm_pct", f"{fw:.2f}"])
        w.writerow(["rate", f"{rate:.0f}", "sum_tail_frac", f"{st:.3f}"])

# --- plots ---
fig, axes = plt.subplots(1, 3, figsize=(16, 4.8))

ax = axes[0]
ax.plot([0, 1400], [0, 1400], ":", color="gray", lw=1, label="ideal (measured=true)")
ax.plot(tk, mk, "o-", color="#3b6ea5", lw=2, label="peak-hold measured")
ax.axvline(FULLSCALE_KEV, color="#c0392b", ls="--", lw=1)
ax.text(FULLSCALE_KEV - 20, 200, f"full scale\n~{FULLSCALE_KEV:.0f} keV", ha="right",
        fontsize=8, color="#c0392b")
ax.axvspan(1150, 1360, alpha=0.10, color="#c0392b")
ax.text(1255, 60, "Co-60\nsaturates", ha="center", fontsize=8, color="#c0392b")
ax.set_xlabel("true energy (keV)"); ax.set_ylabel("measured energy (keV)")
ax.set_title("1. Isolated pulse: exact until clip\n(gain for Cs-137 662 -> Co-60 off-scale)")
ax.legend(fontsize=8); ax.grid(alpha=0.3)

ax = axes[1]
ax.plot(dt_ns_f, nres_f, "o-", color="#4C9F70", lw=2, label="CeBr3 (17 ns) count")
ax.plot(dt_ns_s, nres_s, "s-", color="#C0504D", lw=2, label="GAGG (90 ns) count")
ax.axhline(2, color="gray", ls=":", lw=1); ax.axhline(1, color="gray", ls=":", lw=1)
ax.set_xlabel("pulse separation dt (ns)")
ax.set_ylabel("pulses detected (2 = resolved, 1 = merged)")
ax.set_title("2. Overlap: slow crystal stays merged longer\n(merge = lost count + fake sum energy)")
ax.set_ylim(0.5, 2.2); ax.legend(fontsize=8); ax.grid(alpha=0.3)
axb = ax.twinx()
axb.plot(dt_ns_s, mx_s, "--", color="#C0504D", alpha=0.5, lw=1)
axb.axhline(600, color="k", ls=":", lw=0.7)
axb.set_ylabel("merged max energy (keV), GAGG", color="#C0504D", fontsize=8)
axb.text(dt_ns_s[1], 610, "2x300 = fake 600 keV line", fontsize=7, color="#C0504D")

ax = axes[2]
rates = [r[0] for r in rate_rows]
cen = [r[1] for r in rate_rows]
tail = [r[3] * 100 for r in rate_rows]
ax.semilogx(np.array(rates) / 1e3, cen, "o-", color="#3b6ea5", lw=2, label="662 photopeak centroid")
ax.axhline(662, color="gray", ls=":", lw=1)
ax.set_xlabel("count rate (kcps)"); ax.set_ylabel("photopeak centroid (keV)", color="#3b6ea5")
ax.set_title("3. Rate: centroid drifts + fake sum events grow")
ax.grid(alpha=0.3, which="both")
axt = ax.twinx()
axt.semilogx(np.array(rates) / 1e3, tail, "s--", color="#c0392b", lw=2, label="events >720 keV (%)")
axt.set_ylabel("fake sum-tail fraction >720 keV (%)", color="#c0392b")
lines = ax.get_lines()[:1] + axt.get_lines()[:1]
ax.legend(lines, [l.get_label() for l in lines], fontsize=8, loc="center left")

fig.suptitle("Phase A -- simple peak-hold: where the reconstructed ENERGY cannot be trusted", fontsize=13)
fig.tight_layout(rect=[0, 0, 1, 0.95])
fig.savefig("ballistic_deficit.png", dpi=130)
print("saved ballistic_deficit.png, ballistic_deficit.csv")
