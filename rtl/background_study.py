"""Plot the ambient-background sweep from `montecarlo background` (samples/background_sweep.csv):
how a diffuse, UNCODED background degrades coded-aperture localization as the background-to-signal
ratio (BSR) climbs.

The background is not directionally coded, so it lands as a uniform pedestal on the flood map; the
MURA decode pushes that flat pedestal into the DC term and rejects most of it — localization holds
until the pedestal's SHOT NOISE buries the coded peak. The sweep shows the collapse knee.
"""
import csv
import os
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

HERE = os.path.dirname(os.path.abspath(__file__))
CSV = os.path.join(HERE, "..", "samples", "background_sweep.csv")


def main():
    bsr, rms, snr, fail, bias = [], [], [], [], []
    with open(CSV) as f:
        for row in csv.DictReader(f):
            bsr.append(float(row["bsr"]))
            rms.append(float(row["rms_mm"]))
            snr.append(float(row["peak_snr"]))
            fail.append(100 * float(row["fail_rate"]))
            bias.append(float(row["bias_mm"]))

    fig, ax = plt.subplots(1, 3, figsize=(15, 4.6))

    # ---- Panel 1: localization RMS vs BSR (the collapse) ----
    ax[0].plot(bsr, rms, "o-", color="#2c7fb8", label="localization RMS")
    ax[0].plot(bsr, bias, "s--", color="#7fcdbb", label="high-stats bias", alpha=0.8)
    ax[0].axhline(rms[0], color="gray", ls=":", lw=1, label=f"clean floor {rms[0]:.2f} mm")
    ax[0].set_xlabel("background-to-signal ratio (BSR)")
    ax[0].set_ylabel("localization error (mm)")
    ax[0].set_title("Localization vs ambient background")
    ax[0].legend(fontsize=8)

    # ---- Panel 2: decode contrast (peak SNR) vs BSR ----
    ax[1].plot(bsr, snr, "o-", color="#d95f0e")
    ax[1].set_xlabel("background-to-signal ratio (BSR)")
    ax[1].set_ylabel("recon peak SNR  (peak − mean) / std")
    ax[1].set_title("Decode contrast collapses with background")
    for x, y in zip(bsr, snr):
        ax[1].annotate(f"{y:.1f}", (x, y), textcoords="offset points", xytext=(0, 6), fontsize=7, ha="center")

    # ---- Panel 3: failure rate vs BSR (the operating limit) ----
    ax[2].plot(bsr, fail, "o-", color="#756bb1")
    ax[2].axhline(50, color="gray", ls=":", lw=1, label="50% fail")
    ax[2].set_xlabel("background-to-signal ratio (BSR)")
    ax[2].set_ylabel("% of acquisitions failing (>3 mm)")
    ax[2].set_title("Operating limit (fixed acquisition)")
    ax[2].set_ylim(-2, 102)
    ax[2].legend(fontsize=8)

    fig.suptitle("Ambient background vs coded-aperture localization — uncoded uniform pedestal (MURA rejects DC, shot noise remains)",
                 fontsize=12)
    fig.tight_layout(rect=(0, 0, 1, 0.95))
    out = os.path.join(HERE, "background.png")
    fig.savefig(out, dpi=110)
    print("wrote", out)
    knee = next((b for b, r in zip(bsr, rms) if r > 2.0), None)
    print(f"clean floor {rms[0]:.2f} mm; collapse knee near BSR {knee}")


if __name__ == "__main__":
    main()
