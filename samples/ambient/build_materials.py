"""Soil and dry-air photon interaction data for the AB-4 soil/air generator, from immutable public snapshots.

python samples/ambient/build_materials.py --out samples/ambient/materials-v1.json

Inputs (hash-checked against source-data/v1/materials-sources.json):
  * NIST XCOM element files MDATX3.zzz and ATWTS.DAT from https://physics.nist.gov/PhysRefData/Xcom/XCOM.tar.gz
    (M. J. Berger, J. H. Hubbell, S. M. Seltzer et al., XCOM: Photon Cross Sections Database, NIST Standard
    Reference Database 8 (XGAM)). Partial cross sections in barns/atom; mu/rho = sigma * 0.60221367 / A
    (the XCOM program's own AVOG constant and atomic weights).
  * NIST dry-air mu_en/rho and mu/rho: https://physics.nist.gov/PhysRefData/XrayMassCoef/ComTab/air.html
    (J. H. Hubbell and S. M. Seltzer, NISTIR 5632, 1995).
  * Dry-air composition and density: NIST ESTAR material 104, https://physics.nist.gov/cgi-bin/Star/compos.pl?matno=104
  * Soil: H. L. Beck, J. DeCampo and C. Gogolak, HASL-258 (1972), Table 2 composition by weight (Al2O3 13.5 %,
    Fe2O3 4.5 %, SiO2 67.5 %, CO2 4.5 %, H2O 10 %) and the 1.6 g/cm3 density of its text (p. 10).
No coefficient is tuned: the mixture rule mu/rho = sum w_i (mu/rho)_i is the only operation.
"""
import argparse
import hashlib
import json
import math
import re
from pathlib import Path

HERE = Path(__file__).parent
SOURCE = HERE / "source-data" / "v1"
AVOG = 0.60221367  # XCOM.f DATA AVOG: 1e-24 cm2/barn times Avogadro's number / 1e24
ELEMENTS = {1: "H", 6: "C", 7: "N", 8: "O", 13: "Al", 14: "Si", 18: "Ar", 26: "Fe"}
SOIL_OXIDES = {"Al2O3": 13.5, "Fe2O3": 4.5, "SiO2": 67.5, "CO2": 4.5, "H2O": 10.0}
SOIL_DENSITY = 1.6
AIR_FRACTIONS = {6: 0.000124, 7: 0.755267, 8: 0.231781, 18: 0.012827}  # ESTAR 104 (tab2.html rounds N to 0.755268)
AIR_DENSITY = 1.20479e-3
MAX_KEV = 20000.0


def check_hashes():
    sources = json.loads((SOURCE / "materials-sources.json").read_text(encoding="utf-8"))
    for entry in sources["Files"]:
        digest = hashlib.sha256((SOURCE / entry["File"]).read_bytes()).hexdigest()
        if digest != entry["Sha256"]:
            raise ValueError(f"{entry['File']}: snapshot hash differs; re-audit before use")
    return sources


def atomic_weights():
    text = (SOURCE / "xcom" / "ATWTS.DAT").read_text()
    values = [float(v) for v in re.findall(r"\d+\.\d+", text)]
    return {z: values[z - 1] for z in ELEMENTS}


def read_element(z):
    tokens = (SOURCE / "xcom" / f"MDATX3.{z:03d}").read_text().split()
    pos = 0
    def take():
        nonlocal pos
        pos += 1
        return tokens[pos - 1]
    if int(take()) != z:
        raise ValueError(f"MDATX3.{z:03d}: wrong Z")
    take()
    edges, count = int(take()), int(take())
    _ = [take() for _ in range(edges)]  # grid index of each edge
    _ = [take() for _ in range(edges)]  # edge labels
    _ = [take() for _ in range(edges)]  # edge energies, eV
    energy = [float(take()) / 1000 for _ in range(count)]
    arrays = [[float(take()) for _ in range(count)] for _ in range(5)]
    return energy, arrays  # coherent, incoherent, photoelectric, pair nuclear, pair electron (b/atom)


def interpolate(xs, ys, x):
    """Log-log linear between grid points; linear where an endpoint is zero (below a pair threshold)."""
    if x <= xs[0]:
        return ys[0]
    for i in range(1, len(xs)):
        if x <= xs[i]:
            x0, x1, y0, y1 = xs[i - 1], xs[i], ys[i - 1], ys[i]
            if x == x1:
                return y1
            if y0 <= 0 or y1 <= 0:
                return y0 + (y1 - y0) * (x - x0) / (x1 - x0)
            return math.exp(math.log(y0) + math.log(y1 / y0) * math.log(x / x0) / math.log(x1 / x0))
    return ys[-1]


def soil_fractions(weights):
    formula = {"Al2O3": {13: 2, 8: 3}, "Fe2O3": {26: 2, 8: 3}, "SiO2": {14: 1, 8: 2}, "CO2": {6: 1, 8: 2}, "H2O": {1: 2, 8: 1}}
    total = sum(SOIL_OXIDES.values())
    fractions = {}
    for oxide, percent in SOIL_OXIDES.items():
        mass = sum(n * weights[z] for z, n in formula[oxide].items())
        for z, n in formula[oxide].items():
            fractions[z] = fractions.get(z, 0.0) + percent / total * n * weights[z] / mass
    return fractions


def nist_air_table():
    text = (SOURCE / "nist" / "air.html").read_text(encoding="utf-8", errors="replace")
    ascii_part = text[text.find("ASCII format"):]
    rows = re.findall(r"(\d\.\d{5}E[+-]\d\d)\s+(?:\d+\s+[A-Z]\d?\s+)?(\d\.\d{3}E[+-]\d\d)\s+(\d\.\d{3}E[+-]\d\d)", ascii_part)
    table = [(float(e) * 1000, float(mu), float(muen)) for e, mu, muen in rows]
    if len(table) < 30:
        raise ValueError("NIST air table not parsed")
    return table


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    sources = check_hashes()
    weights = atomic_weights()
    elements = {z: read_element(z) for z in ELEMENTS}
    grid = sorted({e for energy, _ in elements.values() for e in energy if 1.0 <= e <= MAX_KEV})
    fractions = {"Soil": soil_fractions(weights), "Air": AIR_FRACTIONS}
    materials = {}
    for name, frac in fractions.items():
        partial = [[0.0] * len(grid) for _ in range(5)]
        for z, w in frac.items():
            energy, arrays = elements[z]
            for k in range(5):
                for i, e in enumerate(grid):
                    partial[k][i] += w * interpolate(energy, arrays[k], e) * AVOG / weights[z]
        materials[name] = dict(
            DensityGPerCm3=SOIL_DENSITY if name == "Soil" else AIR_DENSITY,
            MassFractions={ELEMENTS[z]: w for z, w in sorted(frac.items())},
            CoherentCm2PerG=partial[0], IncoherentCm2PerG=partial[1], PhotoelectricCm2PerG=partial[2],
            PairNuclearCm2PerG=partial[3], PairElectronCm2PerG=partial[4])
    air = nist_air_table()
    # Data check, not a fit: the composed XCOM air total must reproduce NIST's tabulated air mu/rho at
    # shared grid energies to the table's 4-significant-figure rounding.
    check = []
    for e, mu, _ in air:
        if e in grid and e >= 1.0:
            i = grid.index(e)
            composed = sum(materials["Air"][k][i] for k in ("CoherentCm2PerG", "IncoherentCm2PerG",
                           "PhotoelectricCm2PerG", "PairNuclearCm2PerG", "PairElectronCm2PerG"))
            check.append(dict(EnergyKeV=e, NistMuCm2PerG=mu, ComposedMuCm2PerG=composed, Ratio=composed / mu))
    worst = max(abs(c["Ratio"] - 1) for c in check)
    # K-shell edges of each material's elements, from the same XCOM files (used only by the fluorescence bound).
    edges = {}
    for name, frac in fractions.items():
        edges[name] = []
        for z in frac:
            tokens = (SOURCE / "xcom" / f"MDATX3.{z:03d}").read_text().split()
            n = int(tokens[2])
            labels = tokens[4 + n:4 + 2 * n]
            energies = [float(t) / 1000 for t in tokens[4 + 2 * n:4 + 3 * n]]
            edges[name] += [dict(Element=ELEMENTS[z], EdgeKeV=e) for l, e in zip(labels, energies) if l == "K"]
    out = dict(SchemaVersion=1, Id="soil-hasl258-air-nist-v1", Version=1,
               Sources=sources["Citations"], Interpolation="log-log linear on the XCOM element grids, then on the union grid",
               EnergyKeV=grid, Soil=materials["Soil"], Air=materials["Air"],
               AirMuEnKeV=[e for e, _, _ in air], AirMuEnCm2PerG=[m for _, _, m in air],
               KEdges=edges, AirTotalCheck=check, AirTotalCheckWorstRelativeDifference=worst)
    payload = json.dumps(out, indent=1)
    args.out.write_bytes((payload + "\n").encode("utf-8"))  # LF on every OS: hashed files must not depend on the platform
    print(f"grid {len(grid)} energies; air composed vs NIST worst |ratio-1| = {worst:.2e}")


if __name__ == "__main__":
    main()
