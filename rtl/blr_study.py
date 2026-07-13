"""Baseline-restorer demonstration: the trapezoidal shaper's Q8 pole-zero cancellation leaves a residual
per pulse, so its DC baseline WALKS over a long pulse train (theme 27). baseline_restorer.sv tracks that
walk with a gated leaky integrator and subtracts it. This plots, from the C# MC event stream shaped by the
same integer reference the RTL is bit-exact to (so the curves ARE the RTL behaviour):

  (1) a zoom late in the stream — raw shaped output sitting on a walked-down baseline vs the BLR output
      with a flat zero baseline and the SAME flat-top heights;
  (2) the baseline estimate over the whole train — 0 -> ~-47000 ADC — that the BLR cancels.

Run `montecarlo eventstream <cfg>` first, then `python blr_study.py`.
"""
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

import trap_ref
import event_stream

GATE, FRAC = 4096, 12


def main():
    events, _ = event_stream.read_stream()
    wave = event_stream.rasterize(events)
    shaped = trap_ref.trap_shape(wave)              # walking baseline
    restored = trap_ref.blr(shaped, GATE, FRAC)     # baseline-restored (bit-exact to baseline_restorer.sv)
    base = [s - r for s, r in zip(shaped, restored)]  # the BLR's baseline estimate = shaped - output

    n = len(shaped)
    fig, ax = plt.subplots(1, 2, figsize=(14, 5.0))

    # ---- Panel 1: zoom on a late segment (baseline has walked down by then) ----
    lo = int(n * 0.80)
    hi = min(n, lo + 2500)
    xs = range(lo, hi)
    ax[0].plot(xs, shaped[lo:hi], color="#bbbbbb", lw=1.0, label="raw shaper output (walked baseline)")
    ax[0].plot(xs, restored[lo:hi], color="#2c7fb8", lw=1.0, label="after BLR (flat baseline)")
    ax[0].axhline(0, color="#444", lw=0.8, ls=":")
    ax[0].plot(xs, base[lo:hi], color="#d95f0e", lw=1.2, label="baseline estimate")
    ax[0].set_xlabel("sample"); ax[0].set_ylabel("shaper output (ADC)")
    ax[0].set_title(f"Zoom @ ~{lo} samples: walk removed, flat tops preserved")
    ax[0].legend(fontsize=8, loc="upper right")

    # ---- Panel 2: the baseline walk over the whole train ----
    ax[1].plot(range(n), base, color="#d95f0e", lw=1.0)
    ax[1].axhline(0, color="#444", lw=0.8, ls=":")
    ax[1].set_xlabel("sample"); ax[1].set_ylabel("baseline estimate (ADC)")
    ax[1].set_title(f"Pole-zero baseline walk the BLR cancels (min {min(base)} ADC over {len(events)} events)")

    fig.suptitle("RTL baseline restoration — gated leaky integrator cancels the trapezoidal shaper's pole-zero baseline walk",
                 fontsize=12)
    fig.tight_layout(rect=(0, 0, 1, 0.95))
    fig.savefig("blr.png", dpi=110)
    print("wrote blr.png")
    print(f"raw baseline walk: 0 -> {min(base)} ADC; BLR output baseline stays ~0 (min {min(restored)}, max {max(restored)})")


if __name__ == "__main__":
    main()
