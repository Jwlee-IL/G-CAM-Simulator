"""Tune the pulse RISE time (tau_rise): how it changes the ADC pulse shape, the peak-vs-tail (ballistic)
deficit, the shaper's pole-zero baseline walk, and the energy resolution. The current default (2.0 samples
= 20 ns at 100 MSPS) is slow for a scintillator (rise/fall = 0.4) and suppresses the visible peak to ~1/3
of the tail amplitude; a faster rise (~1 sample) is more realistic.

Run `montecarlo eventstream <cfg>` first, then `python tau_rise_study.py`.
"""
import math
import statistics
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

import trap_ref
import event_stream

ADC = event_stream.ADC
RISES_NS = [(0.5, "#1a9850"), (1.0, "#66bd63"), (1.5, "#fdae61"), (2.0, "#d73027"), (3.0, "#7b3294")]
WALK_CAP = 160_000     # samples of the stream used for the baseline-walk metric (speed)


def pulse_shape(tau_rise, kev=661.7):
    """One isolated pulse (codes) for the plot + its peak as a fraction of the tail amplitude a."""
    a = kev * ADC["adc_per_kev"]
    n, n0 = 60, 5
    p = [ai for ai in trap_ref.biexp_pulse(n, n0, kev, trap_ref.TAU_SAMPLES, tau_rise, ADC["adc_per_kev"])]
    return p[n0:], max(p) / a


def walk_and_fwhm(events, tau_rise):
    capped = [(s, e) for s, e in events if s < WALK_CAP]
    wave = event_stream.rasterize(capped, tau_rise=tau_rise)
    shaped = trap_ref.trap_shape(wave)
    base = [s - r for s, r in zip(shaped, trap_ref.blr(shaped))]
    walk = -min(base)                                        # baseline walk magnitude (ADC)
    k = event_stream.calibrate_flat_per_kev(tau_rise=tau_rise)
    iso = trap_ref.RISE + trap_ref.FLAT + 8 * int(math.ceil(trap_ref.TAU_SAMPLES))
    arr = [s for s, _ in capped]
    rec = []
    for i, (n0, e) in enumerate(capped):
        if e < 620:
            continue
        left = n0 - arr[i - 1] if i > 0 else 10 ** 9
        right = arr[i + 1] - n0 if i + 1 < len(capped) else 10 ** 9
        if min(left, right) >= iso:
            rec.append(event_stream.flat_top(shaped, n0) / k)
    fwhm = 2.3548 * statistics.pstdev(rec) / statistics.mean(rec) * 100 if len(rec) > 20 else float("nan")
    return walk, fwhm


def main():
    events, _ = event_stream.read_stream()

    fig, ax = plt.subplots(1, 3, figsize=(16, 4.8))

    # ---- Panel 1: pulse shapes ----
    a662 = 661.7 * ADC["adc_per_kev"]
    for tr, c in RISES_NS:
        shape, pk = pulse_shape(tr)
        ax[0].plot(range(len(shape)), shape, "-", color=c, lw=1.6,
                   label=f"tau_rise={tr} ({tr*8:.0f} ns), peak {pk*100:.0f}% of tail")
    ax[0].axhline(a662, color="#888", ls=":", lw=1)
    ax[0].text(30, a662 * 0.97, f"tail amplitude a = {a662:.0f} codes (662 keV)", fontsize=8, color="#555", va="top")
    ax[0].set_xlabel("sample after arrival"); ax[0].set_ylabel("ADC codes")
    ax[0].set_title("Pulse shape vs rise time\nfaster rise -> peak recovers toward the tail amplitude")
    ax[0].legend(fontsize=7.5)

    # ---- Panels 2 & 3: metrics vs tau_rise ----
    trs = [tr for tr, _ in RISES_NS]
    peaks = [pulse_shape(tr)[1] * 100 for tr in trs]
    walks, fwhms = [], []
    for tr in trs:
        w, f = walk_and_fwhm(events, tr)
        walks.append(w / 1000.0); fwhms.append(f)

    ax[1].plot(trs, peaks, "o-", color="#2c7fb8", label="peak / tail amplitude")
    ax[1].plot(trs, [100] * len(trs), ":", color="#888")
    ax1b = ax[1].twinx()
    ax1b.plot(trs, walks, "s--", color="#d95f0e", label="baseline walk (flat)")
    ax1b.set_ylim(0, max(walks) * 1.4)      # from 0 so the walk reads as FLAT, not auto-zoomed zigzag
    ax[1].set_ylim(0, 105)
    # ADC sampling floor: below ~0.6 samples the rise bandwidth exceeds Nyquist -> aliases (FPGA can't get it).
    ax[1].axvspan(0.3, 0.6, color="#d73027", alpha=0.12)
    ax[1].text(0.44, 88, "aliases\n(BW > Nyquist)", fontsize=7, color="#a50026", ha="center", rotation=90, va="top")
    ax[1].axvline(1.0, color="#1a9850", lw=1.4); ax[1].text(1.03, 12, "chosen 1.0", fontsize=8, color="#1a9850")
    ax[1].set_xlabel("tau_rise (samples)"); ax[1].set_ylabel("peak (% of tail)", color="#2c7fb8")
    ax1b.set_ylabel("baseline walk (kADC over stream)", color="#d95f0e")
    ax[1].set_title("Deficit + baseline walk vs rise time")
    ax[1].legend(fontsize=8, loc="center right"); ax1b.legend(fontsize=8, loc="lower right")

    ax[2].plot(trs, fwhms, "o-", color="#7b3294")
    ax[2].axvline(1.0, color="#1a9850", lw=1.4)
    ax[2].set_xlabel("tau_rise (samples)"); ax[2].set_ylabel("662 keV photopeak FWHM (%)")
    ax[2].set_title("Energy resolution vs rise time\n(shaper reads the tail -> nearly flat)")
    ax[2].set_ylim(0, max(fwhms) * 1.3)
    for x, y in zip(trs, fwhms):
        ax[2].annotate(f"{y:.1f}%", (x, y), textcoords="offset points", xytext=(0, 6), fontsize=7, ha="center")

    fig.suptitle(f"Pulse rise-time (tau_rise) — {ADC['name']}, 125 MSPS. Chosen 1.0 sample (peak 53% of tail): the "
                 "FASTEST the ADC can cleanly sample (BW < Nyquist); below ~0.6 it aliases. Walk is set by the "
                 "pole-zero tail, NOT the rise.", fontsize=10)
    fig.tight_layout(rect=(0, 0, 1, 0.93))
    fig.savefig("tau_rise.png", dpi=110)
    print("wrote tau_rise.png")
    for tr, pk, w, f in zip(trs, peaks, walks, fwhms):
        print(f"  tau_rise={tr:.1f} ({tr*8:.0f} ns): peak {pk:.0f}% of tail, walk {w:.0f} kADC, FWHM {f:.2f}%")


if __name__ == "__main__":
    main()
