"""Camera / mask PARALLAX in the fused (gamma-overlay-on-video) display.

A common display for gamma cameras: an optical camera overlays the reconstructed gamma
image on live video. The optical camera CANNOT be coaxial with the mask (the mask is
opaque tungsten facing the scene; nothing can sit on the gamma axis), so it is mounted
as close as possible -> a fixed lateral baseline b between the gamma axis and the
camera axis.

Consequence: a point source at distance z is seen along directions that differ by the
parallax angle  Δθ ≈ b / z  (small angle). The gamma blob lands off the visible source
by Δθ on the overlay. It is WORST up close (1/z) and vanishes far away.

Synergy with the depth (z) estimation (findings theme 18-19): b is a known mechanical
constant, so once z is known the reprojection shift b/z can be computed and removed.
And the coded aperture ranges best exactly in the near field where parallax is worst.

Usage:  python samples/camera_parallax_study.py
"""
import os
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))

# gamma angular resolution ~ cellPitch / D (far field); validated config cell 1 mm, D 55 mm
CELL, D = 1.0, 55.0
gamma_res_deg = np.degrees(CELL / D)      # ~1.04 deg — one resolution element
BASELINES = [32.0, 40.0, 60.0]            # mm; 32 ≈ shield radius + camera radius (min for this body)

z = np.linspace(0.15, 8.0, 400) * 1000.0  # source distance, mm (0.15..8 m)

fig, (axg, axp) = plt.subplots(1, 2, figsize=(14, 5.6), gridspec_kw={"width_ratios": [1, 1.35]})

# ---- (left) top-down geometry schematic ----
b = 40.0
zs = 900.0  # a 0.9 m source for the sketch
axg.plot([0, 0], [0, zs], color="#c0392b", lw=2)                    # gamma axis
axg.plot([b, b], [0, 0], marker="s", color="#2e8b57", ms=1)
axg.plot([b, 0], [0, zs], color="#2e8b57", lw=2)                    # camera line-of-sight to source
axg.plot([0, b], [0, 0], color="#333", lw=3)                        # baseline
axg.scatter([0], [zs], s=180, marker="*", color="#e67e22", zorder=5)
axg.text(2, zs, " source", fontsize=9, va="center")
axg.text(-3, zs / 2, "gamma axis\n(mask faces scene)", color="#c0392b", fontsize=8, ha="right", va="center")
axg.text(b / 2, -55, "baseline b", ha="center", fontsize=8)
axg.annotate("", (b, 0), (0, 0), arrowprops=dict(arrowstyle="<->", color="#333"))
# parallax angle arc
th = np.arctan2(b, zs)
axg.text(6, zs * 0.78, f"Δθ = b/z\n≈ {np.degrees(np.arctan(b/zs)):.1f}° @ {zs/1000:.1f} m", color="#2e8b57", fontsize=8)
axg.set_xlim(-60, 90); axg.set_ylim(-90, zs + 90)
axg.set_title("Geometry: camera offset b from the gamma axis\n→ direction to a near source differs by Δθ = b/z", fontsize=9.5)
axg.axis("off")

# ---- (right) parallax misregistration vs distance ----
for b, ls in zip(BASELINES, ["-", "--", ":"]):
    axp.plot(z / 1000.0, np.degrees(np.arctan(b / z)), ls, color="#2e8b57", lw=2, label=f"b = {b:.0f} mm")
axp.axhspan(0, gamma_res_deg, color="#3498db", alpha=0.12)
axp.axhline(gamma_res_deg, color="#3498db", lw=1.2)
axp.text(6.5, gamma_res_deg * 1.1, f"gamma resolution ≈ {gamma_res_deg:.1f}°\n(below = parallax invisible)", color="#2471a3", fontsize=8, va="bottom")
# near-field depth-correctable zone (coded aperture ranges while dM/dS appreciable, ~< 0.4 m)
axp.axvspan(0.15, 0.4, color="#2ecc71", alpha=0.12)
axp.text(0.27, 8, "depth-\ncorrectable\n(coded z)", ha="center", fontsize=7.5, color="#27ae60")
# crossover distance for b=40
zc = 40.0 / np.radians(gamma_res_deg) / 1000.0
axp.annotate(f"b=40mm crosses at ~{zc:.1f} m", (zc, gamma_res_deg), (2.2, 3.0),
             fontsize=8, arrowprops=dict(arrowstyle="->", color="#555"))
axp.set_xlabel("source distance z (m)"); axp.set_ylabel("overlay misregistration Δθ (deg)")
axp.set_yscale("log"); axp.set_ylim(0.2, 25)
axp.set_title("Parallax misregistration vs distance\nworst up close (∝1/z); 'mount close' shrinks b — the right instinct", fontsize=9.5)
axp.legend(fontsize=8, title="camera offset"); axp.grid(alpha=0.3, which="both")

fig.suptitle("Camera–mask parallax in the gamma/video overlay: a known baseline b → Δθ=b/z misregistration. "
             "Worst & depth-correctable up close; negligible far. 'Mount as close as possible' was correct.", fontsize=10.5)
fig.tight_layout(rect=[0, 0, 1, 0.94])
out = os.path.join(here, "camera_parallax.png")
fig.savefig(out, dpi=130); print("saved", out)

# ---- text: crossover table ----
print("\nParallax = one gamma-resolution element at these source distances (worse closer):")
for b in BASELINES:
    zc = b / np.radians(gamma_res_deg)
    print(f"  b={b:4.0f}mm  ->  visible (>{gamma_res_deg:.1f}°) for z < {zc/1000:.1f} m")
print("\nΔθ at fixed distances (b=40mm):")
for zm in (0.3, 0.5, 1.0, 2.0, 5.0):
    print(f"  z={zm:.1f} m  ->  Δθ = {np.degrees(np.arctan(40.0/(zm*1000))):.1f}°")
