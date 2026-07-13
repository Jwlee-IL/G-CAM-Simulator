"""Realistic front-end: the recovered energy spectrum and its resolution budget.

The ADC waveform now carries real front-end effects (event_stream.rasterize): a finite rise (ballistic
deficit), a white electronic-noise floor, per-event intrinsic photostatistics, and ADC clipping — so the
shaper has a real noise floor to fight and the recovered Cs-137 spectrum has a REALISTIC photopeak width
instead of a delta function. This plots (1) the recovered spectrum — 662 photopeak + Compton continuum +
a pile-up tail — and (2) the photopeak FWHM budget: electronic and intrinsic contributions add in
quadrature to a realistic ~6.5% at 662 keV.

Run `montecarlo eventstream <cfg>` first, then `python frontend_study.py`.
"""
import math
import random
import statistics
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

import trap_ref
import event_stream

FS_HZ = 100e6
FLAT = trap_ref.FLAT


def overlay_rate(energies, rate_kcps, seed=2):
    rng = random.Random(seed)
    mean_gap = FS_HZ / (rate_kcps * 1e3)
    t, ev = 0.0, []
    for e in energies:
        t += -math.log(1.0 - rng.random()) * mean_gap
        ev.append((int(round(t)), e))
    return ev


def recover(events, rise, intrinsic_fwhm, noise_kev):
    """(all recovered energies, isolated-photopeak FWHM %) at this ramp length."""
    wave = event_stream.rasterize(events, intrinsic_fwhm=intrinsic_fwhm, noise_kev=noise_kev)
    shaped = trap_ref.trap_shape(wave, rise, FLAT, trap_ref.M_Q8)
    k = event_stream.calibrate_flat_per_kev(rise=rise, flat=FLAT)
    iso = rise + FLAT + 8 * int(math.ceil(trap_ref.TAU_SAMPLES))
    arr = [s for s, _ in events]
    allrec, isopk = [], []
    for i, (n0, e) in enumerate(events):
        r = event_stream.flat_top(shaped, n0, rise=rise, flat=FLAT) / k
        allrec.append(r)
        if e < 620:
            continue
        left = n0 - arr[i - 1] if i > 0 else 10 ** 9
        right = arr[i + 1] - n0 if i + 1 < len(events) else 10 ** 9
        if min(left, right) >= iso:
            isopk.append(r)
    fwhm = None
    if len(isopk) >= 20:
        m, sd = statistics.mean(isopk), statistics.pstdev(isopk)
        fwhm = 2.3548 * sd / m * 100.0
    return allrec, fwhm


def main():
    events, _ = event_stream.read_stream()
    energies = [e for _, e in events]

    fig, ax = plt.subplots(1, 2, figsize=(13, 5.0))

    # ---- Panel 1: the recovered spectrum (realistic photopeak + continuum + pile-up tail) ----
    ev = overlay_rate(energies, 500)
    allrec, fwhm = recover(ev, trap_ref.RISE, trap_ref.INTRINSIC_FWHM, trap_ref.NOISE_KEV)
    bins = [i * 8 for i in range(0, 120)]
    ax[0].hist(energies, bins=bins, histtype="step", color="#999", lw=1.2, label="deposited (MC truth)")
    ax[0].hist(allrec, bins=bins, histtype="stepfilled", color="#2c7fb8", alpha=0.55,
               label="recovered by the shaper")
    ax[0].axvline(661.7, color="#d95f0e", ls=":", lw=1)
    if fwhm:
        ax[0].annotate(f"662 keV photopeak\nFWHM {fwhm:.1f}%", (661.7, 0), (430, 60),
                       fontsize=9, color="#d95f0e", arrowprops=dict(arrowstyle="->", color="#d95f0e"))
    ax[0].set_xlabel("energy (keV)"); ax[0].set_ylabel("events / 8 keV")
    ax[0].set_title("Recovered spectrum @ 500 kcps\nrealistic photopeak width + Compton continuum + pile-up tail")
    ax[0].set_xlim(0, 900); ax[0].legend(fontsize=8)

    # ---- Panel 2: the resolution budget at the default ramp (electronic vs intrinsic vs total) ----
    rows = [
        ("electronic only\n(3 keV ENC)", 0.0, trap_ref.NOISE_KEV, "#2c7fb8"),
        ("intrinsic only\n(6% @ 662)", trap_ref.INTRINSIC_FWHM, 0.0, "#7fbf7b"),
        ("FULL front-end", trap_ref.INTRINSIC_FWHM, trap_ref.NOISE_KEV, "#d95f0e"),
    ]
    labels, vals, cols = [], [], []
    for lab, ifw, nk, c in rows:
        _, f = recover(ev, trap_ref.RISE, ifw, nk)
        labels.append(lab); vals.append(f); cols.append(c)
    bars = ax[1].bar(range(len(labels)), vals, color=cols)
    ax[1].set_xticks(range(len(labels)))
    ax[1].set_xticklabels(labels, fontsize=8)
    ax[1].set_ylabel("662 keV photopeak FWHM (%)")
    ax[1].set_title(f"Resolution budget @ RISE={trap_ref.RISE}\n(electronic + intrinsic add in quadrature)")
    for b, v in zip(bars, vals):
        ax[1].text(b.get_x() + b.get_width() / 2, v + 0.05, f"{v:.2f}%", ha="center", fontsize=8)

    fig.suptitle("Realistic front-end: finite rise + electronic noise + intrinsic photostatistics + ADC clipping "
                 "— the shaper now has a real noise floor to fight", fontsize=11)
    fig.tight_layout(rect=(0, 0, 1, 0.94))
    fig.savefig("frontend.png", dpi=110)
    print("wrote frontend.png")
    for lab, ifw, nk, _ in rows:
        print(f"  {lab.splitlines()[0]:16}: {recover(ev, trap_ref.RISE, ifw, nk)[1]:.2f}% FWHM")


if __name__ == "__main__":
    main()
