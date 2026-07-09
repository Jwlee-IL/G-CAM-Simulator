"""Material rate-capability study: feed each scintillator's decay time (and GAGG
afterglow) into the peak-detector RTL and sweep the count rate, measuring detection
efficiency (pile-up loss) and photopeak resolution. Reuses peak_detector.sv unchanged
— only the stimulus waveform changes per material.

Run from the rtl/ dir with iverilog/vvp on PATH:  python material_rate_study.py
"""
import os
import csv
import subprocess
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

os.chdir(os.path.dirname(os.path.abspath(__file__)))

FS = 100e6            # 100 MHz sampling (10 ns/sample)
BASELINE = 200
NOISE = 3.0
ADC_PER_KEV = 3000.0 / 662.0
ADC_MAX = 4095
COMPTON_EDGE = 477.0
N_EVENTS = 1500

# name: (decay_ns, afterglow_fraction)
MATERIALS = {
    "CeBr3":   (17,  0.000),
    "LYSO":    (40,  0.000),
    "GAGG:Mg": (55,  0.005),
    "GAGG":    (90,  0.050),
    "NaI":     (230, 0.000),
    "BGO":     (300, 0.000),
}
RATES = [50e3, 100e3, 200e3, 500e3, 1e6, 2e6]

subprocess.run(["iverilog", "-g2012", "-o", "sim.vvp", "tb_peak_detector.sv", "peak_detector.sv"], check=True)


def generate(rate, decay_ns, afterglow_frac, seed=12345):
    rng = np.random.default_rng(seed)
    tau_d = max(1.0, decay_ns / 10.0)
    tau_r = max(0.5, tau_d / 5.0)
    L = int(max(64, 8 * tau_d))
    n = np.arange(L)
    shape = np.exp(-n / tau_d) - np.exp(-n / tau_r)
    shape /= shape.max()
    pk = int(np.argmax(shape))

    gaps = rng.exponential(FS / rate, N_EVENTS).astype(int) + 1
    times = np.cumsum(gaps) + 400
    total = int(times[-1] + L + 400)

    E = np.empty(N_EVENTS)
    pp = rng.random(N_EVENTS) < 0.6
    E[pp] = rng.normal(662.0, 0.07 * 662 / 2.355, pp.sum())
    E[~pp] = rng.uniform(30.0, COMPTON_EDGE, (~pp).sum())
    E = np.clip(E, 0.0, None)
    amp = E * ADC_PER_KEV

    wave = np.full(total, float(BASELINE))
    truth = []
    for t, a, e in zip(times, amp, E):
        end = min(t + L, total)
        wave[t:end] += a * shape[:end - t]
        truth.append((t + pk, e))

    # Afterglow: a long slow phosphorescence pool (IIR) fed by each event -> at high rate
    # it accumulates, lifting/roughening the baseline (the GAGG problem).
    if afterglow_frac > 0:
        d = np.exp(-1.0 / 300.0)
        after = np.empty(total)
        acc = 0.0
        inj = np.zeros(total)
        for t, a in zip(times, amp):
            if t < total:
                inj[t] += afterglow_frac * a
        for i in range(total):
            acc = acc * d + inj[i]
            after[i] = acc
        wave += after

    wave += rng.normal(0.0, NOISE, total)
    return np.clip(np.round(wave), 0, ADC_MAX).astype(int), truth


def score(adc, truth):
    np.savetxt("adc.txt", adc, fmt="%d")
    subprocess.run(["vvp", "sim.vvp"], check=True, capture_output=True)
    det = np.loadtxt("peaks.txt") if os.path.getsize("peaks.txt") > 0 else np.zeros((0, 2))
    if det.ndim == 1:
        det = det.reshape(-1, 2)
    tt = np.array([t[0] for t in truth])
    dt = det[:, 0].astype(int)
    da = det[:, 1]

    used = np.zeros(len(tt), bool)
    matched = 0
    for pt in dt:
        j = int(np.argmin(np.abs(tt - pt)))
        if abs(tt[j] - pt) <= 10 and not used[j]:
            used[j] = True
            matched += 1
    eff = matched / len(tt)

    keV = da / ADC_PER_KEV
    near = keV[(keV > 600) & (keV < 720)]
    fwhm = (2.355 * near.std() / near.mean() * 100) if len(near) > 5 else float("nan")
    return eff, fwhm


rows = []
for mat, (decay, ag) in MATERIALS.items():
    for r in RATES:
        adc, truth = generate(r, decay, ag)
        eff, fwhm = score(adc, truth)
        rows.append((mat, decay, ag, r, eff, fwhm))
        print(f"{mat:8s} {r/1e3:6.0f}k/s  eff={eff*100:5.1f}%  FWHM={fwhm:4.1f}%")

with open("material_rate.csv", "w", newline="") as f:
    w = csv.writer(f)
    w.writerow(["material", "decay_ns", "afterglow", "rate_cps", "efficiency", "photopeak_fwhm"])
    for row in rows:
        w.writerow([row[0], row[1], row[2], f"{row[3]:.0f}", f"{row[4]:.4f}", f"{row[5]:.3f}"])

colors = {"CeBr3": "#4C9F70", "LYSO": "#8064A2", "GAGG:Mg": "#E1A140",
          "GAGG": "#C0504D", "NaI": "#999999", "BGO": "#4d4d4d"}
fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(12, 5))
for mat in MATERIALS:
    m = [r for r in rows if r[0] == mat]
    rate = [x[3] / 1e3 for x in m]
    eff = [x[4] * 100 for x in m]
    fw = [x[5] for x in m]
    ax1.semilogx(rate, eff, "o-", color=colors[mat], label=f"{mat} ({MATERIALS[mat][0]}ns)")
    ax2.semilogx(rate, fw, "o-", color=colors[mat], label=mat)
ax1.set_xlabel("count rate (kcps)"); ax1.set_ylabel("detection efficiency (%)")
ax1.set_title("Rate capability: fast crystals resist pile-up"); ax1.legend(fontsize=8); ax1.grid(alpha=0.3, which="both")
ax2.set_xlabel("count rate (kcps)"); ax2.set_ylabel("photopeak FWHM (%)")
ax2.set_title("Energy resolution vs rate (GAGG afterglow degrades)"); ax2.legend(fontsize=8); ax2.grid(alpha=0.3, which="both")
fig.suptitle("Scintillator decay time -> RTL peak-detector rate capability", fontsize=12)
fig.tight_layout()
fig.savefig("material_rate.png", dpi=130)
print("saved material_rate.png, material_rate.csv")
