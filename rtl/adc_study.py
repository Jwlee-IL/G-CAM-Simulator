"""Look at the ADC waveform itself — what the shaper actually receives from the realistic front-end
(event_stream.rasterize): finite-rise bi-exponential pulses whose amplitude carries the deposited energy
(with intrinsic + electronic spread), sitting on a white electronic-noise floor, with pile-up where events
overlap and ADC clipping on the tallest stacks.

  (top)   an overview stretch of the stream — pulses of various heights on the noise floor;
  (lower-left)  zoom on one isolated pulse — the finite rise and exponential fall, resolved sample by sample;
  (lower-right) zoom on a pile-up cluster — two events landing within a shaping window.

Run `montecarlo eventstream <cfg>` first, then `python adc_study.py`.
"""
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

import math
import trap_ref
import event_stream

ADC = event_stream.ADC                                    # active ADC preset (default AD9648 14-bit)
# total per-sample noise = analog/preamp (noise_kev -> codes) in quadrature with the ADC's own noise
SIGMA_ADC = math.sqrt((trap_ref.NOISE_KEV * ADC["adc_per_kev"]) ** 2 + ADC["adc_noise_codes"] ** 2)


def find_isolated(events, guard=60):
    """An isolated ~662 keV event (big gap on both sides), for a clean single-pulse zoom."""
    arr = [s for s, _ in events]
    for i, (n0, e) in enumerate(events):
        if e < 640:
            continue
        left = n0 - arr[i - 1] if i > 0 else 10 ** 9
        right = arr[i + 1] - n0 if i + 1 < len(events) else 10 ** 9
        if min(left, right) > guard and n0 > 5000:
            return n0
    return events[len(events) // 2][0]


def find_pileup(events, sep=25):
    """A pair of events within `sep` samples — a pile-up cluster."""
    arr = [s for s, _ in events]
    for i in range(1, len(arr)):
        if 3 < arr[i] - arr[i - 1] < sep and arr[i] > 5000:
            return arr[i - 1]
    return arr[len(arr) // 2]


def main():
    events, _ = event_stream.read_stream()
    wave = event_stream.rasterize(events)                 # the realistic ADC stream (ints)

    iso = find_isolated(events)
    pile = find_pileup(events)

    fig = plt.figure(figsize=(13, 7.5))
    gs = fig.add_gridspec(2, 2, height_ratios=[1.0, 1.1])
    ax_top = fig.add_subplot(gs[0, :])
    ax_iso = fig.add_subplot(gs[1, 0])
    ax_pile = fig.add_subplot(gs[1, 1])

    # ---- overview ----
    o0 = max(0, iso - 300)
    o1 = min(len(wave), o0 + 1700)
    ax_top.plot(range(o0, o1), wave[o0:o1], color="#2c7fb8", lw=0.8)
    ax_top.axhline(0, color="#888", lw=0.6, ls=":")
    ax_top.axhspan(-2 * SIGMA_ADC, 2 * SIGMA_ADC, color="#d95f0e", alpha=0.12,
                   label=f"electronic noise +/-2 sigma (+/-{2*SIGMA_ADC:.0f} ADC)")
    ax_top.set_title("ADC waveform the shaper receives — realistic front-end (finite rise + noise floor + pile-up)")
    ax_top.set_xlabel("sample (10 ns each)"); ax_top.set_ylabel("ADC counts")
    ax_top.legend(fontsize=8, loc="upper right")

    # ---- isolated single pulse ----
    lo = iso - 15
    hi = iso + 70
    ax_iso.plot(range(lo, hi), wave[lo:hi], "-o", color="#2c7fb8", ms=3, lw=0.9)
    ax_iso.axhline(0, color="#888", lw=0.6, ls=":")
    ax_iso.axhspan(-2 * SIGMA_ADC, 2 * SIGMA_ADC, color="#d95f0e", alpha=0.12)
    pk = max(wave[lo:hi])
    ax_iso.annotate("finite rise\n(ballistic deficit)", (iso, pk * 0.5), (iso - 13, pk * 0.62),
                    fontsize=8, color="#333", arrowprops=dict(arrowstyle="->"))
    ax_iso.annotate("exponential fall (tau)", (iso + 12, pk * 0.35), (iso + 22, pk * 0.55),
                    fontsize=8, color="#333", arrowprops=dict(arrowstyle="->"))
    ax_iso.set_title(f"Zoom: one isolated ~662 keV pulse (peak {pk} ADC)")
    ax_iso.set_xlabel("sample"); ax_iso.set_ylabel("ADC counts")

    # ---- pile-up cluster ----
    lo = pile - 15
    hi = pile + 90
    ax_pile.plot(range(lo, hi), wave[lo:hi], "-o", color="#7b3294", ms=3, lw=0.9)
    ax_pile.axhline(0, color="#888", lw=0.6, ls=":")
    ax_pile.axhspan(-2 * SIGMA_ADC, 2 * SIGMA_ADC, color="#d95f0e", alpha=0.12)
    ax_pile.set_title("Zoom: a pile-up cluster (two events within a shaping window)")
    ax_pile.set_xlabel("sample"); ax_pile.set_ylabel("ADC counts")

    fig.suptitle(f"What the ADC outputs to the shaper — {ADC['name']}, full scale {ADC['full_scale_kev']:.0f} keV "
                 f"({ADC['bits']}-bit, +/-{ADC['adc_max']} codes)", fontsize=12)
    fig.tight_layout(rect=(0, 0, 1, 0.96))
    fig.savefig("adc.png", dpi=110)
    print(f"wrote adc.png  [{ADC['name']}]")
    print(f"gain {ADC['adc_per_kev']:.2f} codes/keV; 662 keV -> {662*ADC['adc_per_kev']:.0f} codes "
          f"({100*662*ADC['adc_per_kev']/ADC['adc_max']:.0f}% FS); noise sigma {SIGMA_ADC:.1f} codes "
          f"(ADC-only {ADC['adc_noise_codes']:.1f})")


if __name__ == "__main__":
    main()
