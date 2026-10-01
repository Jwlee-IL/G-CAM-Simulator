"""New hardware structure for the handheld coded-aperture locator — a concept layout
that folds in every result from this session:

  * optical head : rank-7 MURA W mask (muzzle) + D=55mm standoff + 16x16 GAGG:Mg 15mm
  * shield       : tungsten cup, thickness = the weight/volume knob (8mm~2kg .. 12mm~3kg)
  * readout      : SiPM + front-end just behind the crystal (pixel 256ch or monolithic ~30ch)
  * camera + ToF : co-mounted ON TOP of the muzzle (baseline b minimal, on ONE axis -> 1-D
                   parallax); ToF rangefinder gives z for exact b/z overlay correction
  * balance      : CoM sits over a pistol grip just behind the head; battery in the rear
                   tail acts as counterweight to the forward shield mass
  * display      : a Windows tablet that docks on the tail for aiming and undocks for remote use over
                   Wi-Fi (decisions D-15, D-26 in docs/VV.Gcam.Decisions.md)

Two views: (left) side elevation with subsystems + center of mass; (right) muzzle face.
Usage:  python samples/hardware_concept.py
"""
import os
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import FancyBboxPatch, Circle, Rectangle, Polygon

here = os.path.dirname(os.path.abspath(__file__))

# --- mass model (g) for the CoM, reusing the design study's breakdown (12mm shield build) ---
masses = {  # (x_mm from muzzle, mass_g)
    "shield":   (70.0, 2500.0),   # tungsten cup over the optical head
    "mask":     (5.0,   40.0),
    "crystal":  (72.0,  40.0),
    "readout":  (95.0, 150.0),
    "daq_fpga": (150.0, 350.0),
    "battery":  (185.0, 300.0),
    "housing":  (120.0, 300.0),
    "cam_tof":  (10.0,  120.0),
}
M = sum(m for _, m in masses.values())
com_x = sum(x * m for x, m in masses.values()) / M
print(f"total ~{M/1000:.2f} kg, CoM at x={com_x:.0f} mm from the muzzle -> put the grip there")

fig, (ax, axf) = plt.subplots(1, 2, figsize=(14.5, 6.2), gridspec_kw={"width_ratios": [2.3, 1]})

# ================= side elevation =================
Hh = 50.0                       # head outer height (mm) ~ Ø50 body
yc = 60.0                       # vertical centre of the barrel

def box(x, w, y, h, fc, ec="k", lw=1.2, label=None, fs=7.5, alpha=1.0, rounded=False):
    p = FancyBboxPatch((x, y), w, h, boxstyle="round,pad=0,rounding_size=3" if rounded else "square,pad=0",
                       fc=fc, ec=ec, lw=lw, alpha=alpha)
    ax.add_patch(p)
    if label:
        ax.text(x + w / 2, y + h / 2, label, ha="center", va="center", fontsize=fs)

# shield cup over the optical head (front section)
box(0, 110, yc - Hh/2, Hh, "#8d99a0", ec="k", lw=1.4)                       # head body / shield
box(0, 10, yc - (Hh-8)/2, Hh-8, "#34495e", label="mask\n(muzzle)", fs=6.5) # W mask at muzzle
box(10, 62, yc - (Hh-16)/2, Hh-16, "#eef2f3", label="D=55mm\nstandoff", fs=7)  # coded gap (air)
box(72, 15, yc - (Hh-16)/2, Hh-16, "#2ecc71", label="GAGG\n15mm", fs=6.5)  # crystal
box(87, 20, yc - (Hh-18)/2, Hh-18, "#3498db", label="SiPM\n+FE", fs=6.5)   # readout
ax.text(34, yc + Hh/2 + 4, "tungsten shield: thickness is the weight knob (8 mm ≈ 2 kg … 12 mm ≈ 3 kg; CoM for 12 mm)",
        ha="left", fontsize=7, color="#555")

# rear tail: DAQ/FPGA + battery counterweight
box(110, 90, yc - Hh/2 + 4, Hh - 8, "#dfe6e9", ec="k")                      # rear housing
box(118, 40, yc - 12, 24, "#9b59b6", label="DAQ+FPGA", fs=6.5)
box(162, 34, yc - 12, 24, "#e67e22", label="battery\n(counterwt)", fs=6)
box(112, 84, yc + Hh/2 + 14, 10, "#d6eaf8", label="Windows tablet — docks here, undocks over Wi-Fi", fs=6)
ax.text(200, yc, "))) Wi-Fi", fontsize=8, color="#2471a3", va="center")

# camera + ToF pod on top of the muzzle (minimal baseline b, single axis)
box(0, 30, yc + Hh/2, 16, "#c0392b", label="cam + ToF", fs=6.5, alpha=0.9)
ax.annotate("", (5, yc + Hh/2 + 16), (5, yc), arrowprops=dict(arrowstyle="<->", color="#c0392b", lw=1))
ax.text(-2, yc + Hh/2 + 8, "b (small,\non one axis)", fontsize=6.5, color="#c0392b", ha="right", va="center")

# pistol grip under the CoM
grip = Polygon([(com_x-16, yc - Hh/2), (com_x+16, yc - Hh/2),
                (com_x+26, yc - Hh/2 - 62), (com_x+2, yc - Hh/2 - 62)], closed=True,
               fc="#5d6d7e", ec="k", lw=1.3)
ax.add_patch(grip)
ax.text(com_x+6, yc - Hh/2 - 34, "grip\n+trigger", ha="center", va="center", fontsize=7, color="w")

# center of mass marker
ax.add_patch(Circle((com_x, yc), 4.5, fc="w", ec="k", lw=1.2, zorder=6))
ax.add_patch(Polygon([(com_x-4.5, yc), (com_x, yc+4.5), (com_x+4.5, yc)], fc="k"))
ax.add_patch(Polygon([(com_x-4.5, yc), (com_x, yc-4.5), (com_x+4.5, yc)], fc="k"))
ax.text(com_x, yc + 10, f"CoM\n@{com_x:.0f}mm", ha="center", fontsize=7, weight="bold")

# scene / photons arrow
ax.annotate("γ from scene", (-30, yc), (-8, yc), fontsize=8, color="#7f8c8d",
            arrowprops=dict(arrowstyle="->", color="#7f8c8d"), va="center", ha="right")

# overall dimension
ax.annotate("", (0, yc - Hh/2 - 72), (200, yc - Hh/2 - 72), arrowprops=dict(arrowstyle="<->", lw=1.2))
ax.text(100, yc - Hh/2 - 78, "≈ 200 mm", ha="center", va="top", fontsize=9, weight="bold")
ax.text(100, 128, "Recommended: pistol-grip 'locator' — heavy shielded head over the hand, "
        "battery tail counterweights, dockable tablet screen", ha="center", fontsize=9)

ax.set_xlim(-45, 235); ax.set_ylim(-25, 135); ax.axis("off")
ax.set_aspect("equal")

# ================= muzzle face view =================
R = 25.0
axf.add_patch(Circle((0, 0), R, fc="#8d99a0", ec="k", lw=1.4))          # head OD
axf.add_patch(Rectangle((-9, -9), 18, 18, fc="#34495e", ec="k"))        # mask aperture (14mm sq)
# MURA-ish pattern hint
rng = [(-6,-6),(0,-6),(6,0),(-6,3),(3,3),(3,-3),(-3,6)]
for (px,py) in rng:
    axf.add_patch(Rectangle((px-1.5,py-1.5),3,3, fc="#eef2f3", ec="none"))
axf.text(0, -14, "rank-7 MURA mask", ha="center", fontsize=7, color="w")
# camera + ToF on top, on the vertical axis (1-D parallax)
axf.add_patch(Circle((0, R-6), 4.5, fc="#c0392b", ec="k"))
axf.text(9, R-6, "camera", fontsize=7, va="center", color="#c0392b")
axf.add_patch(Circle((-9, R-6), 3, fc="#e67e22", ec="k"))
axf.text(-11, R-6, "ToF", fontsize=7, va="center", ha="right", color="#b9770e")
axf.annotate("", (0, 0), (0, R-6), arrowprops=dict(arrowstyle="<->", color="#c0392b", lw=1))
axf.text(1.5, (R-6)/2, "b", fontsize=8, color="#c0392b")
axf.set_xlim(-30, 30); axf.set_ylim(-30, 30); axf.axis("off"); axf.set_aspect("equal")
axf.set_title("Muzzle face\ncam+ToF on ONE axis above the mask\n→ parallax is 1-D, corrected by b/z", fontsize=9)

fig.suptitle("New hardware structure: a balance-first pistol-grip locator. Shielded optical head over the grip, "
             "camera+rangefinder on the muzzle for 1-D parallax correction, battery as counterweight.", fontsize=10.5)
fig.tight_layout(rect=[0, 0, 1, 0.93])
out = os.path.join(here, "hardware_concept.png")
fig.savefig(out, dpi=130); print("saved", out)
