"""Compare three digital pulse shapers on the realistic ADC waveform — CR-RC^4, trapezoidal, and a finite
cusp — to show the fundamental noise-vs-pile-up tradeoff that only becomes visible once the front-end has a
real electronic-noise floor and real pulse pile-up (themes 30).

  (1) impulse responses — the flat-top (trap), the semi-Gaussian (CR-RC), and the sharp cusp;
  (2) NOISE: electronic-noise-only photopeak FWHM (isolated events) — cusp is near-optimal, trapezoid pays a
      little noise for its flat top;
  (3) PILE-UP: fraction of 662 keV events whose energy is recovered within +/-5% (NO isolation cut, full
      front-end) vs count rate. Pile-up follows the total SUPPORT WIDTH, so the flat-top trapezoid — widest
      here — degrades FASTEST; its flat top buys ballistic-deficit immunity, not rate. (This is offline
      energy-recovery accuracy, not a hardware trigger/dead-time throughput.)

Run `montecarlo eventstream <cfg>` first, then `python shaper_compare.py`.
"""
import math
import random
import statistics
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

import trap_ref
import event_stream
import shapers

FS_HZ = 100e6
# roughly shaping-time-matched settings (support ~20-28 samples)
TRAP = dict(rise=trap_ref.RISE, flat=trap_ref.FLAT)
CRRC = dict(order=4, tau_s=2.5)
CUSP = dict(half=10, tau_c=4.0)
COLORS = {"trapezoid": "#2c7fb8", "CR-RC^4": "#d95f0e", "cusp": "#7b3294"}

# noiseless isolated reference pulse -> per-shaper gain + peak latency
_REF = event_stream.rasterize([(20, 661.7)], noise_kev=0, intrinsic_fwhm=0, tail_pad=120)
_RT = trap_ref.trap_shape(_REF, TRAP["rise"], TRAP["flat"], trap_ref.M_Q8)
_RC = shapers.crrc(_REF, **CRRC)
_RU = shapers.cusp(_REF, **CUSP)
GAIN = {"trapezoid": max(_RT) / 661.7, "CR-RC^4": max(_RC) / 661.7, "cusp": max(_RU) / 661.7}
PKLAT = {"CR-RC^4": _RC.index(max(_RC)) - 20, "cusp": _RU.index(max(_RU)) - 20}


def overlay(energies, rate_kcps, seed=3):
    rng = random.Random(seed)
    mg = FS_HZ / (rate_kcps * 1e3)
    t, ev = 0.0, []
    for e in energies:
        t += -math.log(1.0 - rng.random()) * mg
        ev.append((int(round(t)), e))
    return ev


def shaped_of(kind, wave):
    if kind == "trapezoid":
        return trap_ref.trap_shape(wave, TRAP["rise"], TRAP["flat"], trap_ref.M_Q8)
    if kind == "CR-RC^4":
        return shapers.crrc(wave, **CRRC)
    return shapers.cusp(wave, **CUSP)


def read_energy(kind, shaped, n0):
    if kind == "trapezoid":
        return event_stream.flat_top(shaped, n0) / GAIN[kind]
    p = PKLAT[kind]
    return shapers.peak_in(shaped, n0, p - 3, p + 4) / GAIN[kind]


def recover(kind, ev, intrinsic, isolated):
    wave = event_stream.rasterize(ev, intrinsic_fwhm=intrinsic)
    shaped = shaped_of(kind, wave)
    arr = [s for s, _ in ev]
    iso = 40
    rec = []
    for i, (n0, e) in enumerate(ev):
        if e < 620:
            continue
        if isolated:
            left = n0 - arr[i - 1] if i > 0 else 10 ** 9
            right = arr[i + 1] - n0 if i + 1 < len(ev) else 10 ** 9
            if min(left, right) < iso:
                continue
        rec.append(read_energy(kind, shaped, n0))
    return rec


def main():
    events, _ = event_stream.read_stream()
    energies = [e for _, e in events]

    fig, ax = plt.subplots(1, 3, figsize=(16, 4.7))

    # ---- Panel 1: impulse responses (normalized to unit peak) ----
    for kind, ref in [("trapezoid", _RT), ("CR-RC^4", _RC), ("cusp", _RU)]:
        pk = max(ref)
        seg = [v / pk for v in ref[15:70]]
        ax[0].plot(range(len(seg)), seg, color=COLORS[kind], lw=1.8, label=kind)
    ax[0].set_xlabel("sample after arrival"); ax[0].set_ylabel("shaper output (norm.)")
    ax[0].set_title("Impulse response\nflat-top vs semi-Gaussian vs cusp")
    ax[0].legend(fontsize=9)

    # ---- Panel 2: electronic-noise FWHM (isolated, intrinsic OFF) ----
    ev100 = overlay(energies, 100)
    noise_fwhm = {}
    for kind in COLORS:
        rec = recover(kind, ev100, 0.0, isolated=True)
        m, sd = statistics.mean(rec), statistics.pstdev(rec)
        noise_fwhm[kind] = 2.3548 * sd / m * 100
    bars = ax[1].bar(range(3), [noise_fwhm[k] for k in COLORS], color=[COLORS[k] for k in COLORS])
    ax[1].set_xticks(range(3)); ax[1].set_xticklabels(list(COLORS), fontsize=9)
    ax[1].set_ylabel("electronic-noise photopeak FWHM (%)")
    ax[1].set_title("NOISE (low rate, intrinsic off)\ncusp near-optimal; trapezoid pays for its flat top")
    for b, k in zip(bars, COLORS):
        ax[1].text(b.get_x() + b.get_width() / 2, noise_fwhm[k] + 0.02, f"{noise_fwhm[k]:.2f}%", ha="center", fontsize=8)

    # ---- Panel 3: throughput vs rate (pile-up, no isolation cut) ----
    rates = [100, 300, 600, 1000, 2000]
    for kind in COLORS:
        thru = []
        for r in rates:
            rec = recover(kind, overlay(energies, r), trap_ref.INTRINSIC_FWHM, isolated=False)
            good = sum(1 for v in rec if abs(v - 661.7) <= 0.05 * 661.7)
            thru.append(100 * good / len(rec))
        ax[2].plot(rates, thru, "-o", color=COLORS[kind], label=kind)
    ax[2].set_xscale("log")
    ax[2].set_xlabel("count rate (kcps)"); ax[2].set_ylabel("662 keV recovered within +/-5% (%)")
    ax[2].set_title("PILE-UP (full front-end)\npile-up ~ SUPPORT WIDTH; the trapezoid's flat top adds width")
    ax[2].legend(fontsize=9); ax[2].set_ylim(0, 100)

    fig.suptitle("Pulse-shaper comparison on the realistic ADC stream: cusp gives the best noise (near-optimal ENC); "
                 "the trapezoid's flat top costs a little noise AND width (more pile-up) — it buys ballistic-deficit "
                 "immunity instead (not exercised by this fixed-rise model).", fontsize=10)
    fig.tight_layout(rect=(0, 0, 1, 0.94))
    fig.savefig("shaper_compare.png", dpi=110)
    print("wrote shaper_compare.png")
    print("electronic-noise FWHM:", {k: round(v, 2) for k, v in noise_fwhm.items()})


if __name__ == "__main__":
    main()
