"""Energy-recovery and pile-up analysis of the trapezoidal shaper driven by the C# Monte Carlo
event stream (rtl/event_stream.txt).

The cocotb test proves the RTL processes the MC stream bit-for-bit like trap_ref; this study uses
that same integer reference (so the curves ARE the RTL behaviour) to answer the physics question:
how well does the flat-top height recover each event's DEPOSITED energy, and how does Poisson
pile-up corrupt it as the count rate climbs?

The deposited-energy SPECTRUM is MC physics (photopeak + Compton continuum, read from the stream
file). The count RATE is an operating-point knob: we re-overlay a Poisson arrival process on the
same deposit list at several rates, rasterize, shape, and read each event's flat top. Run
`montecarlo eventstream <cfg>` first to (re)generate the stream.
"""
import math
import random
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

import trap_ref
import event_stream

FS_HZ = 100e6                       # 100 MSPS front-end (matches the CLI default)
RATES_KCPS = [100, 500, 2000]       # operating count rates to sweep
ISO = trap_ref.RISE + trap_ref.FLAT + 8 * int(math.ceil(trap_ref.TAU_SAMPLES))  # isolation span (samples)
K = event_stream.calibrate_flat_per_kev()   # flat-top ADC units per keV (RTL gain)


def overlay_rate(energies, rate_kcps, seed=1):
    """Re-time the MC deposit list as a Poisson arrival process at rate_kcps; return sorted events."""
    rng = random.Random(seed)
    mean_gap = FS_HZ / (rate_kcps * 1e3)
    t = 0.0
    ev = []
    for e in energies:
        t += -math.log(1.0 - rng.random()) * mean_gap
        ev.append((int(round(t)), e))
    return ev


def recover(events):
    """Shape the rasterized waveform and read each event's flat top -> (true, recovered, is_piled)."""
    wave = event_stream.rasterize(events)
    shaped = trap_ref.trap_shape(wave)
    arr = [s for s, _ in events]
    out = []
    for i, (n0, e_true) in enumerate(events):
        left = n0 - arr[i - 1] if i > 0 else 10 ** 9
        right = arr[i + 1] - n0 if i + 1 < len(events) else 10 ** 9
        piled = min(left, right) < ISO
        e_rec = event_stream.flat_top(shaped, n0) / K
        out.append((e_true, e_rec, piled))
    return out


def main():
    events, meta = event_stream.read_stream()
    energies = [e for _, e in events]
    print(f"MC stream: {len(energies)} events, gain {K:.2f} flat-ADC/keV, isolation span {ISO} samples")

    swept = {r: recover(overlay_rate(energies, r)) for r in RATES_KCPS}

    fig, ax = plt.subplots(1, 3, figsize=(15, 4.6))

    # ---- Panel 1: recovered vs true deposited energy (mid rate) ----
    mid = RATES_KCPS[len(RATES_KCPS) // 2]
    rec = swept[mid]
    cl = [(t, r) for t, r, p in rec if not p]
    pl = [(t, r) for t, r, p in rec if p]
    ax[0].plot([0, 700], [0, 700], "k--", lw=1, label="ideal (recovered = deposited)")
    if cl:
        ax[0].scatter([t for t, _ in cl], [r for _, r in cl], s=9, c="#2c7fb8", alpha=0.6, label=f"isolated ({len(cl)})")
    if pl:
        ax[0].scatter([t for t, _ in pl], [r for _, r in pl], s=12, c="#d95f0e", alpha=0.7, label=f"piled-up ({len(pl)})")
    ax[0].set_xlabel("deposited energy (keV, MC truth)")
    ax[0].set_ylabel("recovered flat-top energy (keV)")
    ax[0].set_title(f"Shaper recovery @ {mid} kcps")
    ax[0].set_xlim(0, 700); ax[0].set_ylim(0, 900)
    ax[0].legend(fontsize=8, loc="upper left")

    # ---- Panel 2: recovered energy spectrum vs rate (pile-up grows a sum tail past 662) ----
    bins = [i * 10 for i in range(0, 96)]
    ax[1].hist(energies, bins=bins, histtype="step", color="k", lw=1.4, label="MC deposit (truth)")
    for r in RATES_KCPS:
        recs = [e for _, e, _ in swept[r]]
        ax[1].hist(recs, bins=bins, histtype="step", lw=1.2, label=f"recovered @ {r} kcps")
    ax[1].axvline(662, color="gray", ls=":", lw=1)
    ax[1].set_xlabel("energy (keV)")
    ax[1].set_ylabel("events / 10 keV")
    ax[1].set_title("Recovered spectrum vs count rate")
    ax[1].legend(fontsize=8)

    # ---- Panel 3: recovery quality vs rate ----
    rates, frac_good, frac_over = [], [], []
    for r in RATES_KCPS:
        recs = swept[r]
        n = len(recs)
        good = sum(1 for t, rr, _ in recs if t > 50 and abs(rr - t) <= 0.05 * t)
        over = sum(1 for t, rr, _ in recs if rr > 662 * 1.05)   # inflated past the photopeak = pile-up sum
        rates.append(r); frac_good.append(100 * good / n); frac_over.append(100 * over / n)
    ax[2].plot(rates, frac_good, "o-", color="#2c7fb8", label="recovered within ±5% (>50 keV)")
    ax[2].plot(rates, frac_over, "s-", color="#d95f0e", label="inflated >662·1.05 (pile-up sum)")
    ax[2].set_xscale("log")
    ax[2].set_xlabel("count rate (kcps)")
    ax[2].set_ylabel("% of events")
    ax[2].set_title("Recovery quality vs rate")
    ax[2].set_ylim(0, 100)
    ax[2].legend(fontsize=8)
    for x, y in zip(rates, frac_good):
        ax[2].annotate(f"{y:.0f}%", (x, y), textcoords="offset points", xytext=(0, 6), fontsize=7, ha="center")

    fig.suptitle("Trapezoidal shaper (RTL) driven by the C# Monte Carlo event stream — energy recovery & pile-up",
                 fontsize=12)
    fig.tight_layout(rect=(0, 0, 1, 0.96))
    fig.savefig("event_stream.png", dpi=110)
    print("wrote event_stream.png")
    for r in RATES_KCPS:
        recs = swept[r]
        good = 100 * sum(1 for t, rr, _ in recs if t > 50 and abs(rr - t) <= 0.05 * t) / len(recs)
        piled = 100 * sum(1 for _, _, p in recs if p) / len(recs)
        print(f"  {r:>5} kcps: {good:5.1f}% recovered ±5%,  {piled:5.1f}% piled-up")


if __name__ == "__main__":
    main()
