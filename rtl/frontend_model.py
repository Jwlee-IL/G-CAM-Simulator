"""SiPM + ADC front-end characteristics model — what specs are optimal for this system.

Energy resolution is set by the photoelectron budget:
    N_pe = lightYield[ph/keV] · E[keV] · collectionEff · PDE
    R_stat(FWHM%) = 235.5 · sqrt(ENF / N_pe)          (photon counting statistics)
    R_total = sqrt(R_stat^2 + R_intrinsic^2)          (+ crystal non-proportionality floor)

The knee shows where the system stops being photon-limited (PDE matters) and becomes
crystal-limited (more PDE is wasted). Prints the optimal-characteristics summary.
"""
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

E = 662.0
COLLECTION = 0.60     # crystal -> SiPM light collection
ENF = 1.20            # SiPM excess noise factor

# name: (light yield ph/keV, intrinsic resolution %, emission peak nm)
CRYSTALS = {
    "GAGG":  (50, 5.0, 520),
    "CeBr3": (45, 3.2, 370),
    "LaBr3": (63, 2.2, 380),
    "LYSO":  (30, 7.0, 420),
    "BGO":   (9,  8.0, 480),
    "NaI":   (38, 5.5, 415),
}
COLORS = {"GAGG": "#C0504D", "CeBr3": "#4C9F70", "LaBr3": "#3b6ea5",
          "LYSO": "#8064A2", "BGO": "#4d4d4d", "NaI": "#999999"}

pde = np.linspace(0.10, 0.60, 60)
fig, ax = plt.subplots(figsize=(8.5, 5.5))
for name, (ly, rintr, nm) in CRYSTALS.items():
    npe = ly * E * COLLECTION * pde
    rstat = 235.5 * np.sqrt(ENF / npe)
    rtot = np.sqrt(rstat ** 2 + rintr ** 2)
    ax.plot(pde * 100, rtot, "-", color=COLORS[name], lw=2, label=f"{name} (LY {ly}, {nm}nm)")
    ax.axhline(rintr, color=COLORS[name], lw=0.7, ls=":", alpha=0.5)  # crystal-limited floor

ax.axvspan(40, 50, alpha=0.08, color="green")
ax.text(45, 11, "typical\nMPPC PDE", ha="center", fontsize=8, color="green")
ax.set_xlabel("SiPM PDE (%)  — at the crystal's emission wavelength")
ax.set_ylabel("energy resolution FWHM @662 keV (%)")
ax.set_title("Front-end: PDE × light yield sets energy resolution\n"
             "(dotted = crystal-limited floor; past the knee, more PDE is wasted)")
ax.set_ylim(0, 14); ax.legend(fontsize=8); ax.grid(alpha=0.3)
fig.tight_layout()
fig.savefig("frontend_resolution.png", dpi=130)

print("Energy resolution @662 keV at PDE = 45% (good MPPC):")
print("  crystal   N_pe    R_stat   R_intr   R_total   regime")
for name, (ly, rintr, nm) in CRYSTALS.items():
    npe = ly * E * COLLECTION * 0.45
    rstat = 235.5 * np.sqrt(ENF / npe)
    rtot = (rstat ** 2 + rintr ** 2) ** 0.5
    regime = "crystal-limited" if rintr > rstat else "photon-limited"
    print(f"  {name:6s}  {npe:6.0f}  {rstat:5.1f}%   {rintr:4.1f}%   {rtot:5.1f}%   {regime}")
print("saved frontend_resolution.png")
