"""Empirical open-fraction sweep with RANDOM coded-aperture arrays + a balanced decode,
confirming theme 21's analytical result and showing why MURA is used.

Self-contained coding Monte-Carlo (no C# geometry): a periodic p×p basic pattern (MURA or
random at open fraction rho), mosaicked 2×2, casts a cyclic point-source shadow + uniform
detector background; Poisson noise; a balanced (mean-subtracted) cyclic cross-correlation
decode locates the source. Detector-background-limited regime (uniform bg dominant).

Shows: (A) random-array coding SNR peaks near rho=0.5 and follows sqrt(rho*(1-rho));
(B) at the SAME rho=0.5, MURA has ~zero sidelobes (clean delta autocorrelation) while a
random array has finite sidelobes -> MURA localizes where random fails.

Usage:  python samples/open_fraction_study.py
"""
import os
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))
rng = np.random.default_rng(12345)
P = 11                    # prime rank (11 → smoother statistics than 7)
MOS = 2                   # 2×2 mosaic
N = P * MOS

# ---- MURA basic (Gottesman–Fenimore) ----
def mura_basic(p):
    qr = {(x * x) % p for x in range(1, p)}
    def C(k): return 1 if k in qr else -1
    A = np.zeros((p, p))
    for x in range(p):
        for y in range(p):
            if x == 0:      A[x, y] = 0
            elif y == 0:    A[x, y] = 1
            else:           A[x, y] = 1 if C(x) * C(y) == 1 else 0
    return A

def mosaic(basic):
    return np.tile(basic, (MOS, MOS))

MURA = mosaic(mura_basic(P))

def decode(d, A):
    """Balanced cyclic cross-correlation (mean-subtracted decoder) via FFT."""
    D = d - d.mean(); AA = A - A.mean()
    return np.fft.ifft2(np.fft.fft2(D) * np.conj(np.fft.fft2(AA))).real

def trial(A, flux, bg):
    """Place a point source at a random cell, cast the shadow, Poisson, decode; return
    (localized correctly?, reconstruction SNR = peak / off-peak RMS)."""
    sx, sy = int(rng.integers(P)), int(rng.integers(P))
    shadow = flux * np.roll(np.roll(A, sx, 0), sy, 1) + bg
    d = rng.poisson(shadow)
    R = decode(d, A)
    # collapse to one period (cyclic) for the peak search
    Rp = R.reshape(MOS, P, MOS, P).mean(axis=(0, 2))
    peak = np.unravel_index(np.argmax(Rp), Rp.shape)
    ok = (peak == (sx, sy))
    off = Rp.copy(); off[sx, sy] = np.nan
    snr = (Rp[sx, sy] - np.nanmean(off)) / (np.nanstd(off) + 1e-9)
    return ok, snr

FLUX, BG = 40.0, 20.0     # per-open-cell source flux and per-pixel uniform background (detector-bg-limited)
N_ARR, N_TRIAL = 40, 40

rhos = np.linspace(0.1, 0.9, 17)
snr_rand = []
for rho in rhos:
    snr_sum, n = 0.0, 0
    for _ in range(N_ARR):
        basic = (rng.random((P, P)) < rho).astype(float)
        if basic.sum() == 0:
            continue
        A = mosaic(basic)
        for _ in range(N_TRIAL):
            _, snr = trial(A, FLUX, BG)
            snr_sum += snr; n += 1
    snr_rand.append(snr_sum / n)
snr_rand = np.array(snr_rand)

# MURA reference at rho=0.5
snr_sum = 0.0
for _ in range(N_ARR * N_TRIAL):
    _, snr = trial(MURA, FLUX, BG)
    snr_sum += snr
mura_snr = snr_sum / (N_ARR * N_TRIAL)
mura_rho = MURA.mean()

# One representative reconstruction each (same source, same noise level) for the visual
def one_recon(A):
    sx, sy = P // 2, P // 2
    shadow = FLUX * np.roll(np.roll(A, sx, 0), sy, 1) + BG
    d = np.random.default_rng(7).poisson(shadow)
    R = decode(d, A).reshape(MOS, P, MOS, P).mean(axis=(0, 2))
    return R / R.max(), (sx, sy)
rand_basic = (np.random.default_rng(3).random((P, P)) < 0.5).astype(float)
R_mura, src = one_recon(MURA)
R_rand, _ = one_recon(mosaic(rand_basic))

fig = plt.figure(figsize=(14.5, 5.2))
gs = fig.add_gridspec(1, 3, width_ratios=[1.5, 1, 1])
ax1 = fig.add_subplot(gs[0, 0]); axm = fig.add_subplot(gs[0, 1]); axr = fig.add_subplot(gs[0, 2])

# (1) coding SNR vs rho
ax1.plot(rhos, snr_rand, "o-", color="#3b6ea5", lw=2, label="random array (empirical)")
coding = np.sqrt(rhos * (1 - rhos)); coding *= snr_rand.max() / coding.max()
ax1.plot(rhos, coding, "--", color="#95a5a6", lw=1.5, label="∝ √(ρ(1−ρ)) (theme 21)")
ax1.scatter([mura_rho], [mura_snr], s=140, marker="*", color="#c0392b", zorder=5, label=f"MURA @ρ={mura_rho:.2f}")
ax1.axvline(0.5, ls=":", color="#e67e22")
ax1.set_xlabel("mask open fraction ρ"); ax1.set_ylabel("reconstruction SNR (peak / sidelobe RMS)")
ax1.set_title(f"(1) Random coding SNR peaks at ρ≈0.5 (√(ρ(1−ρ)))\nMURA {mura_snr:.0f} vs random-best {snr_rand.max():.0f} — ~{mura_snr/snr_rand.max():.0f}× cleaner")
ax1.legend(fontsize=8); ax1.grid(alpha=0.3)

# (2)(3) reconstruction maps at rho=0.5
for ax, R, title in [(axm, R_mura, "MURA (ρ=0.5)"), (axr, R_rand, "random array (ρ=0.5)")]:
    im = ax.imshow(R.T, origin="lower", cmap="magma", vmin=0, vmax=1)
    ax.plot(src[0], src[1], "o", mfc="none", mec="#2ecc71", ms=16, mew=2)
    ax.set_title(title, fontsize=9.5); ax.set_xticks([]); ax.set_yticks([])
axm.text(0.5, -0.09, "clean single peak", transform=axm.transAxes, ha="center", fontsize=8, color="#2e8b57")
axr.text(0.5, -0.09, "peak + sidelobe grass", transform=axr.transAxes, ha="center", fontsize=8, color="#c0392b")

fig.suptitle("Open fraction, empirically: random arrays confirm the √(ρ(1−ρ)) coding power (optimum ρ≈0.5 in the "
             "detector-bg regime), but MURA's zero-sidelobe design beats any random array at the same ρ.", fontsize=9.5)
fig.tight_layout(rect=[0, 0, 1, 0.93])
out = os.path.join(here, "open_fraction.png")
fig.savefig(out, dpi=130); print("saved", out)
print(f"random peak SNR at rho={rhos[np.argmax(snr_rand)]:.2f}; MURA SNR {mura_snr:.1f} vs random-best {snr_rand.max():.1f}")
