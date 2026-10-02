"""Handheld coded-aperture locator: WEIGHT vs PERFORMANCE design synthesis.

A "3 kg large-flashlight" gamma-source locator. This is an ANALYTICAL design layer
on top of the already-validated Monte-Carlo laws (rank/pitch/D localization, stopping-
power efficiency, geometric sensitivity) — it does not re-run the MC. It answers:
which crystal (GAGG:Ce,Mg vs BGO), how big an array, how long a barrel, and what does
it weigh?

Key physical facts it encodes (all found earlier in this project):
  * Position is uniformity-robust and energy windows only need to SEPARATE isotope
    lines -> trade energy resolution for density/ruggedness (GAGG:Mg, BGO both fine).
  * Detection efficiency = 1 - exp(-mu(E)*t_c); mu(E) = mu_662 * (661.7/E)^1.56.
  * Angular resolution ~ cellPitch / D ; cell has a floor (Nyquist vs position res).
  * At ~3 kg the SHIELD (tungsten cup that defines the FOV) dominates the weight, not
    the crystal (tens of grams). So crystal choice acts on weight THROUGH the barrel
    length it allows (denser -> thinner crystal -> shorter barrel -> lighter shield).

Usage:  python samples/handheld_design_study.py
"""
import os
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

here = os.path.dirname(os.path.abspath(__file__))

# --- material constants (mu at 662 keV from samples/materials/*.json; rho, g/cm3) ---
RHO_W = 19.25                       # tungsten (mask + shield)
CRYSTALS = {
    # name         mu662(/mm)  rho    color
    "GAGG:Ce,Mg": dict(mu662=0.057, rho=6.63, col="#2e8b57"),
    "BGO":        dict(mu662=0.063, rho=7.13, col="#b8860b"),
}
LINES = {"Co-57 122": 122.0, "Cs-137 662": 661.7, "Co-60 1173": 1173.0, "Co-60 1332": 1332.0}

def mu(mu662, E):                   # energy-scaled linear attenuation (validated MuRel law)
    return mu662 * (661.7 / E) ** 1.56

def stop_frac(mu662, tc, E):        # photon interaction (detection) fraction in the crystal
    return 1.0 - np.exp(-mu(mu662, E) * tc)

# --- readout architectures: set position sampling (-> min usable cell pitch) & channels ---
ARCH = {
    # name          pos_res_mm (Nyquist half)   sipm_pitch_mm  marker
    "pixel":      dict(pos=1.0, sipm=1.0, mk="o"),   # 1 mm pixels, one channel each (row/col MUX)
    "monolithic": dict(pos=1.5, sipm=3.0, mk="^"),   # continuous Anger, coarse SiPM tiles
}

T_MASK = 8.0        # mask tungsten thickness (mm); ~50% of cell volume is open
T_SHIELD = 12.0     # shield wall/back thickness (mm); the master weight knob (~2.5 HVL @662)
OPEN = 0.5          # MURA open fraction

def design(cryst, arch, a_mm, D_mm, tc_mm, ts_mm=T_SHIELD):
    """One design point. a=detector half-size, D=mask->detector standoff, tc=crystal depth."""
    c = CRYSTALS[cryst]; ar = ARCH[arch]
    det = 2.0 * a_mm                       # square detector side (mm)
    cell = 2.0 * ar["pos"]                 # min usable mask cell pitch (Nyquist on position res)
    barrel = D_mm + tc_mm + 8.0            # cavity height the shield must enclose (mm)

    # --- weights (grams): volume(mm3) * rho(g/cm3) / 1000 ---
    w_crystal = det * det * tc_mm * c["rho"] / 1000.0
    w_mask    = det * det * T_MASK * OPEN * RHO_W / 1000.0
    wall_area = 4.0 * (det + ts_mm) * barrel          # 4 side walls, outer perimeter
    w_shield  = (wall_area * ts_mm + (det + 2*ts_mm) ** 2 * ts_mm) * RHO_W / 1000.0
    channels  = (det / ar["sipm"]) ** 2
    w_read    = channels * 0.3                          # SiPM + front-end per channel (g)
    w_fixed   = 600.0                                   # housing + battery + FPGA + optics
    total_kg  = (w_crystal + w_mask + w_shield + w_read + w_fixed) / 1000.0

    # --- performance ---
    eps662 = stop_frac(c["mu662"], tc_mm, 661.7)
    # sensitivity (relative counts, arbitrary units): collection area * open * stopping
    sens = det * det * OPEN * eps662
    ang_res_mrad = cell / D_mm * 1000.0                # min resolvable angular separation
    fov_deg = 2.0 * np.degrees(np.arctan((det / 2.0) / D_mm))
    return dict(total_kg=total_kg, w_crystal=w_crystal, w_mask=w_mask, w_shield=w_shield,
                w_read=w_read, w_fixed=w_fixed, sens=sens, eps662=eps662,
                ang=ang_res_mrad, fov=fov_deg, det=det, D=D_mm, tc=tc_mm, cell=cell,
                eps1332=stop_frac(c["mu662"], tc_mm, 1332.0))

# ------------------------------------------------------------------ sweeps
a_vals = np.arange(6, 17, 2)        # detector half-size 6..16 mm  -> 12..32 mm array
D_vals = np.arange(30, 91, 10)      # standoff 30..90 mm
TC = {"GAGG:Ce,Mg": 15.0, "BGO": 12.0}   # crystal depth: BGO denser -> thinner for same stop

# normalize sensitivity to the reference geometry (12 mm det, D60, GAGG 10 mm)
ref = design("GAGG:Ce,Mg", "pixel", 6.0, 60.0, 10.0)
S0 = ref["sens"]

points = []
for cryst in CRYSTALS:
    for arch in ARCH:
        for a in a_vals:
            for D in D_vals:
                d = design(cryst, arch, a, D, TC[cryst])
                d["cryst"], d["arch"] = cryst, arch
                d["sens_rel"] = d["sens"] / S0
                points.append(d)

# ------------------------------------------------------------------ figure
fig, axes = plt.subplots(2, 2, figsize=(14.5, 10.5))
ax1, ax2, ax3, ax4 = axes.ravel()

# (1) weight breakdown: reference vs optimized picks
picks = [
    ("original-ish\n(GAGG 12mm,D60)", design("GAGG:Ce,Mg", "pixel", 6.0, 60.0, 10.0)),
    ("opt GAGG:Mg\n(20mm,D55)",       design("GAGG:Ce,Mg", "monolithic", 10.0, 55.0, 15.0)),
    ("opt BGO\n(20mm,D55)",           design("BGO", "monolithic", 10.0, 55.0, 12.0)),
]
comp = ["w_shield", "w_mask", "w_crystal", "w_read", "w_fixed"]
clabels = ["shield (W)", "mask (W)", "crystal", "readout", "housing+batt"]
ccols = ["#7f8c8d", "#34495e", "#2ecc71", "#3498db", "#bdc3c7"]
bottom = np.zeros(len(picks))
for k, lab, col in zip(comp, clabels, ccols):
    vals = np.array([p[1][k] / 1000.0 for p in picks])
    ax1.bar(range(len(picks)), vals, bottom=bottom, label=lab, color=col)
    bottom += vals
ax1.axhline(3.0, ls="--", color="#c0392b", lw=1.4)
ax1.text(2.35, 3.03, "3 kg", color="#c0392b", fontsize=9, ha="right")
ax1.set_xticks(range(len(picks))); ax1.set_xticklabels([p[0] for p in picks], fontsize=8)
ax1.set_ylabel("mass (kg)"); ax1.set_title("(1) Weight budget — the SHIELD dominates")
ax1.legend(fontsize=7.5, loc="upper left"); ax1.grid(axis="y", alpha=0.3)
for i, p in enumerate(picks):
    ax1.text(i, p[1]["total_kg"] + 0.03, f"{p[1]['total_kg']:.2f}", ha="center", fontsize=8, weight="bold")

# (2) pareto: sensitivity vs weight (color=crystal, marker=arch)
for cryst in CRYSTALS:
    for arch in ARCH:
        sub = [p for p in points if p["cryst"] == cryst and p["arch"] == arch]
        w = [p["total_kg"] for p in sub]; s = [p["sens_rel"] for p in sub]
        ax2.scatter(w, s, s=22, marker=ARCH[arch]["mk"], color=CRYSTALS[cryst]["col"],
                    alpha=0.6, edgecolors="none",
                    label=f"{cryst} / {arch}")
ax2.axvline(3.0, ls="--", color="#c0392b", lw=1.2); ax2.text(3.03, 0.5, "3 kg", color="#c0392b", fontsize=9)
ax2.set_xlabel("total mass (kg)"); ax2.set_ylabel("sensitivity  (× reference geometry)")
ax2.set_title("(2) Sensitivity vs weight\n(bigger detector = more counts AND more shield)")
ax2.legend(fontsize=7, loc="upper left"); ax2.grid(alpha=0.3)

# (3) pixel vs monolithic: angular resolution vs sensitivity (marker size = mass), under 3.3 kg
for cryst in CRYSTALS:
    for arch in ARCH:
        sub = [p for p in points if p["cryst"] == cryst and p["arch"] == arch and p["total_kg"] <= 3.3]
        ang = [p["ang"] for p in sub]; s = [p["sens_rel"] for p in sub]
        sz = [(p["total_kg"]) ** 2 * 12 for p in sub]
        ax3.scatter(ang, s, s=sz, marker=ARCH[arch]["mk"], color=CRYSTALS[cryst]["col"],
                    alpha=0.4, edgecolors="none", label=f"{cryst} / {arch}")
ax3.set_xlabel("angular resolution (mrad)  —  smaller = sharper")
ax3.set_ylabel("sensitivity (× ref)")
ax3.set_title("(3) Pixel vs monolithic under ~3 kg\npixel = finer cell → sharper (◯); mono coarsens direction (△)\n(marker size ∝ mass)")
ax3.legend(fontsize=7); ax3.grid(alpha=0.3)
ax3.invert_xaxis()

# (4) the master weight knob: total mass vs SHIELD thickness, for a few detector sizes
ts_vals = np.arange(4, 18.1, 1.0)
for a, ls in [(6.0, ":"), (8.0, "--"), (10.0, "-")]:
    m = [design("GAGG:Ce,Mg", "monolithic", a, 55.0, 15.0, ts) ["total_kg"] for ts in ts_vals]
    ax4.plot(ts_vals, m, ls, color="#2e8b57", lw=2, label=f"detector {2*a:.0f}mm")
ax4.axhline(3.0, ls="--", color="#c0392b", lw=1.2); ax4.text(4.2, 3.05, "3 kg", color="#c0392b", fontsize=9)
ax4.axvspan(4, 6, color="#e67e22", alpha=0.10); ax4.text(5, 1.2, "thin\n(~1 HVL)\nless FOV\ncontrast", ha="center", fontsize=7, color="#b9770e")
ax4.axvspan(10, 14, color="#3498db", alpha=0.08); ax4.text(12, 1.2, "thick\n(~2.5 HVL)\nclean FOV", ha="center", fontsize=7, color="#2471a3")
ax4.set_xlabel("shield wall thickness (mm tungsten)"); ax4.set_ylabel("total mass (kg)")
ax4.set_title("(4) Shield thickness = the master weight knob\n(2/3 of the kg; trade FOV contrast ↔ how much detector fits)")
ax4.legend(fontsize=8); ax4.grid(alpha=0.3)

fig.suptitle("Handheld coded-aperture locator: at ~3 kg the crystal is ~20–40 g — the SHIELD & barrel set the weight. "
             "Pick crystal for density+ruggedness; size the array by Nyquist + channels + shield.", fontsize=11.5)
fig.tight_layout(rect=[0, 0, 1, 0.95])
out = os.path.join(here, "handheld_design.png")
fig.savefig(out, dpi=130)
print("saved", out)

# ------------------------------------------------------------------ text summary
def show(tag, d):
    print(f"\n{tag}: {d['cryst']} / {d['arch']}  det={d['det']:.0f}mm  D={d['D']:.0f}mm  tc={d['tc']:.0f}mm  cell={d['cell']:.1f}mm")
    print(f"   mass = {d['total_kg']:.2f} kg  (shield {d['w_shield']/1000:.2f} + mask {d['w_mask']/1000:.2f}"
          f" + crystal {d['w_crystal']/1000:.3f} + readout {d['w_read']/1000:.3f} + fixed {d['w_fixed']/1000:.2f})")
    print(f"   sensitivity = {d['sens_rel']:.2f}x ref   stop@662 = {d['eps662']*100:.0f}%   stop@1332 = {d['eps1332']*100:.0f}%"
          f"   ang.res = {d['ang']:.1f} mrad   FOV = {d['fov']:.0f} deg")

print("\n" + "=" * 78)
show("REFERENCE (original-ish)", {**ref, "cryst": "GAGG:Ce,Mg", "arch": "pixel", "sens_rel": 1.0})
show("RECOMMENDED GAGG:Ce,Mg", design("GAGG:Ce,Mg", "monolithic", 10.0, 55.0, 15.0) | {"cryst": "GAGG:Ce,Mg", "arch": "monolithic", "sens_rel": design("GAGG:Ce,Mg", "monolithic", 10.0, 55.0, 15.0)["sens"] / S0})
show("ALT BGO (min barrel)", design("BGO", "monolithic", 10.0, 55.0, 12.0) | {"cryst": "BGO", "arch": "monolithic", "sens_rel": design("BGO", "monolithic", 10.0, 55.0, 12.0)["sens"] / S0})
print("=" * 78)

# ================================================================ physical ENVELOPE / VOLUME
# Axial stack-up (mm) for the recommended in-line "flashlight" body.
def envelope(a_mm, D_mm, tc_mm, ts_mm=T_SHIELD, th_house=4.0):
    det = 2 * a_mm
    segs = [("mask (W)", 10.0), ("standoff gap", D_mm), ("crystal", tc_mm),
            ("SiPM + front-end", 18.0), ("digitizer+FPGA+power", 55.0),
            ("battery pack", 35.0), ("caps/window/connector", 18.0)]
    length = sum(s[1] for s in segs)
    width = det + 2 * ts_mm + 2 * th_house            # square cross-section side (mm)
    dia = width * np.sqrt(2)                            # circumscribing round housing (mm)
    v_box = width * width * length / 1000.0            # cm3
    v_cyl = np.pi * (width / 2.0) ** 2 * length / 1000.0
    w_shield_cm3 = design("GAGG:Ce,Mg", "monolithic", a_mm, D_mm, tc_mm, ts_mm)["w_shield"] / 1000.0 / RHO_W * 1000.0
    return dict(segs=segs, length=length, width=width, dia=dia, v_box=v_box, v_cyl=v_cyl,
                det=det, ts=ts_mm, w_shield_cm3=w_shield_cm3)

env = envelope(8.0, 55.0, 15.0, 12.0)   # recommended: 16 mm detector, D55, 15 mm crystal, 12 mm shield
print("\nPHYSICAL ENVELOPE (recommended, in-line body, 12 mm shield, 4 mm housing):")
print(f"  cross-section : {env['width']:.0f} x {env['width']:.0f} mm square  (~Ø{env['dia']:.0f} mm if round)")
print(f"  length        : {env['length']:.0f} mm")
print("  axial stack   : " + " + ".join(f"{n} {l:.0f}" for n, l in env["segs"]) + " mm")
print(f"  volume        : {env['v_box']:.0f} cm3 (box) / {env['v_cyl']:.0f} cm3 (cylinder)  ~ {env['v_box']/1000:.2f} L")
print(f"  (tungsten shield alone occupies ~{env['w_shield_cm3']:.0f} cm3 of dense material)")
print("  shield thickness trade (16 mm detector, D55, 15 mm crystal):")
for ts in (6, 8, 10, 12, 15):
    e = envelope(8.0, 55.0, 15.0, ts); m = design("GAGG:Ce,Mg", "monolithic", 8.0, 55.0, 15.0, ts)["total_kg"]
    print(f"    shield {ts:2d} mm -> {e['width']:.0f} mm across (Ø{e['dia']:.0f}), {e['v_box']/1000:.2f} L, {m:.2f} kg")

# --- dimensioned side-view sketch ---
figE, (axS, axX) = plt.subplots(1, 2, figsize=(13.5, 4.6), gridspec_kw={"width_ratios": [2.4, 1]})
x = 0.0; H = env["width"]
seg_cols = {"mask (W)": "#34495e", "standoff gap": "#ecf0f1", "crystal": "#2ecc71",
            "SiPM + front-end": "#3498db", "digitizer+FPGA+power": "#9b59b6",
            "battery pack": "#e67e22", "caps/window/connector": "#95a5a6"}
# shield walls (top & bottom bands) run mask->crystal region; housing is the full outline
axS.add_patch(plt.Rectangle((0, 0), env["length"], H, fill=False, ec="k", lw=1.5))          # housing outline
core_len = env["segs"][0][1] + env["segs"][1][1] + env["segs"][2][1]
axS.add_patch(plt.Rectangle((0, 0), core_len, env["ts"], color="#7f8c8d", alpha=0.7))         # bottom shield
axS.add_patch(plt.Rectangle((0, H - env["ts"]), core_len, env["ts"], color="#7f8c8d", alpha=0.7))  # top shield
axS.text(core_len / 2, env["ts"] / 2, "tungsten shield", ha="center", va="center", fontsize=7, color="w")
for name, L in env["segs"]:
    axS.add_patch(plt.Rectangle((x, env["ts"] if name in ("standoff gap", "crystal", "mask (W)") else 0),
                                L, (H - 2 * env["ts"]) if name in ("standoff gap", "crystal", "mask (W)") else H,
                                color=seg_cols[name], alpha=0.85, ec="k", lw=0.4))
    axS.text(x + L / 2, H + 3, name.split(" ")[0], ha="center", va="bottom", fontsize=6.5, rotation=90)
    axS.annotate("", (x, -4), (x + L, -4), arrowprops=dict(arrowstyle="<->", color="#555", lw=0.8))
    axS.text(x + L / 2, -9, f"{L:.0f}", ha="center", va="top", fontsize=6.5, color="#555")
    x += L
axS.annotate("", (0, -20), (env["length"], -20), arrowprops=dict(arrowstyle="<->", color="k", lw=1.2))
axS.text(env["length"] / 2, -25, f"total length {env['length']:.0f} mm", ha="center", va="top", fontsize=9, weight="bold")
axS.set_xlim(-8, env["length"] + 8); axS.set_ylim(-34, H + 22); axS.axis("off")
axS.set_title("Recommended handheld — in-line side view (photons enter left, through the mask)", fontsize=9.5)

# cross-section
cx = env["width"] / 2
axX.add_patch(plt.Rectangle((0, 0), env["width"], env["width"], fc="#dfe6e9", ec="k", lw=1.3))     # housing
axX.add_patch(plt.Rectangle((4, 4), env["width"] - 8, env["width"] - 8, fc="#7f8c8d", ec="k"))     # shield
inner = env["det"]
axX.add_patch(plt.Rectangle((cx - inner / 2, cx - inner / 2), inner, inner, fc="#2ecc71", ec="k"))  # crystal
axX.text(cx, cx, f"crystal\n{inner:.0f}mm", ha="center", va="center", fontsize=7)
axX.annotate("", (0, -3), (env["width"], -3), arrowprops=dict(arrowstyle="<->", lw=1))
axX.text(cx, -6, f"{env['width']:.0f} mm  (Ø{env['dia']:.0f} if round)", ha="center", va="top", fontsize=8)
axX.text(4, env["width"] - 6, "12 mm W shield", fontsize=6.5)
axX.set_xlim(-4, env["width"] + 4); axX.set_ylim(-12, env["width"] + 4); axX.axis("off")
axX.set_title(f"cross-section\n~{env['v_box']/1000:.2f} L, ~3 kg", fontsize=9.5)

figE.suptitle(f"Physical envelope: ~Ø{env['dia']:.0f} × {env['length']:.0f} mm, ~{env['v_box']/1000:.2f} L, ~3 kg "
              f"(avg density ~{3.0/(env['v_box']/1000)/1e-3/1000:.1f} g/cm³ — the tungsten shield makes it dense for its size).",
              fontsize=10.5)
figE.tight_layout(rect=[0, 0, 1, 0.93])
outE = os.path.join(here, "handheld_envelope.png")
figE.savefig(outE, dpi=130); print("\nsaved", outE)
