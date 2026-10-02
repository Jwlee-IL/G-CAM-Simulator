"""Check generated CsI data independently and write CsI_checks.txt beside the tables.

Run after gen_crystal_tables.py, with xraylib 4.3.0 on the import path.
Only reads repository inputs and writes the evidence report; no package installation.
"""
import hashlib
import importlib.metadata
import json
import math
from pathlib import Path
import platform
import re
import runpy
import urllib.request

import xraylib as x

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
lines = []
failures = []


def record(message):
    lines.append(str(message))


def check(condition, message):
    record(("PASS: " if condition else "FAIL: ") + message)
    if not condition:
        failures.append(message)


def near(actual, expected, tolerance, message):
    check(abs(actual - expected) <= tolerance,
          f"{message}: actual={actual:.12g}, expected={expected:.12g}, tolerance={tolerance:.12g}")


version = importlib.metadata.version("xraylib")
check(version == "4.3.0", "xraylib version is exactly 4.3.0")
record(f"Python {platform.python_version()}; numpy {importlib.metadata.version('numpy')}")
record("Generation: Python313/python.exe -I -B -c 'import os,sys,runpy; "
       "sys.path.insert(0,os.path.join(os.environ[\"TEMP\"],\"gcam-csi-data\",\"packages\")); "
       "runpy.run_path(\"gen_crystal_tables.py\")' (cwd samples/materials)")
record("Check: same isolated import setup, runpy.run_path(\"check_csi_tables.py\")")
for name in ("gen_crystal_tables.py", "evidence/crystal_tables.json", "check_csi_tables.py",
             "nist-xcom/MDATX3.053", "nist-xcom/MDATX3.055", "nist-xcom/XCOM.f", "nist-xcom/ATWTS.DAT"):
    record(f"SHA256 {name}: {hashlib.sha256((HERE / name).read_bytes()).hexdigest()}")
for package, package_version in (("xraylib", version), ("numpy", importlib.metadata.version("numpy"))):
    with urllib.request.urlopen(f"https://pypi.org/pypi/{package}/{package_version}/json") as response:
        metadata = json.load(response)
    wheel = next(u for u in metadata["urls"] if u["filename"].endswith("cp313-cp313-win_amd64.whl"))
    record(f"PyPI wheel metadata for installed version/tag: {wheel['filename']}; SHA256 {wheel['digests']['sha256']}")

tables = json.loads((HERE / "evidence/crystal_tables.json").read_text())
csi = next(m for m in tables if m["key"] == "CsI")
E, mu, pf = (csi[k] for k in ("E", "mu", "pf"))
check(len(E) == len(mu) == len(pf) == 24, "CsI has 24 equal-length grid/attenuation/fraction nodes")
check(E == sorted(set(E)), "energy grid is sorted and unique")
check(all(math.isfinite(v) and v > 0 for v in mu), "attenuation is finite and positive")
check(all(math.isfinite(v) and 0 < v < 1 for v in pf), "photo fractions are finite and strictly between zero and one")
check(csi["rho"] == 4.51 and csi["formula"] == "CsI", "density 4.51 g/cm3 and undoped CsI host formula")
record("Tl activator omitted, as for NaI:Tl; no concentration or transport error assumed.")
record("Units: E keV; photo, incoherent, coherent and mu cm2/g; linear attenuation 1/mm.")
record("Sources: https://physics.nist.gov/PhysRefData/XrayMassCoef/tab2.html ; "
       "https://physics.nist.gov/PhysRefData/XrayMassCoef/ComTab/cesium.html")
record("Model excludes coherent and pair production; above 800 keV uses KN electrons/g and the 600-800 photo slope.")

composition = x.CompoundParser("CsI")
record(f"xraylib composition: {composition}")
for z in composition["Elements"]:
    edge = x.EdgeEnergy(z, x.K_SHELL)
    below, above = [round(edge * (1 + sign * 1e-4), 4) for sign in (-1, 1)]
    check(below in E and above in E, f"Z={z}, K edge {edge:.8g} straddled at {below}/{above}")
    i, j = E.index(below), E.index(above)
    check(mu[j] > mu[i] and pf[j] > pf[i], f"Z={z} upward attenuation and photo-fraction jumps")
    record(f"Edge Z={z}: mu {mu[i]:.12g} -> {mu[j]:.12g}, ratio {mu[j]/mu[i]:.12g}; "
           f"pf {pf[i]:.12g} -> {pf[j]:.12g}")

# Execute the actual generator so the extension functions under test are its own.
g = runpy.run_path(str(HERE / "gen_crystal_tables.py"))
record("NIST node checks: like totals <=800; documented extension and model < NIST total above 800.")
record("Low-energy tolerance: measured CsI maximum relative like-total residual 0.000105808471, "
       "rounded upward to 0.00011, plus each NIST displayed half-unit and C# mu half-unit.")
record("Above 800 keV, extension error versus NIST retained photo+incoherent is measured, not an acceptance band.")
record("NIST XCOM 3.1 source: https://physics.nist.gov/PhysRefData/Xcom/XCOM.tar.gz?download=1 ; "
       "archive SHA256 b2bdd0608bbece4809a337b7264c85b7997f608a1f8e2181ce5201421efcc0de")
record("Raw files retained in nist-xcom. Exact nodes only, no interpolation; components in barns/atom. "
       "Conversion uses XCOM.f AVOG=0.60221367 and atomic weights from ATWTS.DAT; "
       "Cs/I mass fractions 0.511549/0.488451 from NIST Table 2.")


def nist_components(energy):
    values = [0.0] * 5
    weights_source = (HERE / "nist-xcom/ATWTS.DAT").read_text().splitlines()[1:]
    atomic_weights = [float(v) for line in weights_source if line.strip()
                      for v in line[6:].replace("/", "").split(",") if v.strip()]
    for z, weight in ((53, .488451), (55, .511549)):
        raw = (HERE / f"nist-xcom/MDATX3.{z:03}").read_text().splitlines()
        n = int(raw[1].split()[1])
        tokens = " ".join(raw[5:]).split()
        i = [float(t) for t in tokens[:n]].index(energy * 1000)
        factor = .60221367 * weight / atomic_weights[z - 1]
        for component in range(5):
            token = tokens[(component + 1)*n + i]
            values[component] += factor * float(token)
    return values


for energy, total in ((300, .1818), (600, .08373), (800, .06769), (1000, .05848), (1250, .05110)):
    model = mu[E.index(energy)]
    record(f"{energy} keV: model={model:.12g}, NIST total={total:.12g}, residual={(model/total-1)*100:.9g}%")
    nist_half_unit = 5e-5 if energy == 300 else 5e-6
    csharp_half_unit = 5e-6 if energy == 300 else 5e-7
    if energy <= 800:
        coherent = x.CS_Rayl_CP("CsI", energy)
        tolerance = total * .00011 + nist_half_unit + csharp_half_unit
        near(model + coherent, total, tolerance, f"{energy} keV like-total agreement")
        record(f"Like-total check: coherent={coherent:.12g}, model+coherent={model+coherent:.12g}, "
               f"residual={((model+coherent)/total-1)*100:.9g}%")
    else:
        components = nist_components(energy)
        omitted = components[0] + components[3] + components[4]
        record(f"NIST XCOM components: coherent={components[0]:.15g}; incoherent={components[1]:.15g}; "
               f"photo={components[2]:.15g}; nuclear pair={components[3]:.15g}; electron pair={components[4]:.15g}; "
               f"omitted={omitted:.15g}")
        check(model < total, f"{energy} keV model/NIST={model/total:.12g} < 1")
        retained = components[1] + components[2]
        record(f"Analytic extension versus NIST retained photo+incoherent: "
               f"{model-retained:.15g} cm2/g ({(model/retained-1)*100:.9g}%). "
               "Measured approximation error of the documented extension, not a precision guarantee.")

p = x.CS_Photo_CP("CsI", 661.7)
c = x.CS_Compt_CP("CsI", 661.7)
element_p = sum(w * x.CS_Photo(z, 661.7) for z, w in zip(composition["Elements"], composition["massFractions"]))
element_c = sum(w * x.CS_Compt(z, 661.7) for z, w in zip(composition["Elements"], composition["massFractions"]))
# Same weighted sum via distinct elemental API calls: allow only double arithmetic error.
near(p, element_p, 1e-14, "photo compound versus elemental mixture (floating-point allowance)")
near(c, element_c, 1e-14, "Compton compound versus elemental mixture (floating-point allowance)")
record(f"661.7 keV: p={p:.15g}; c={c:.15g}; mu={p+c:.15g}; pf={p/(p+c):.15g}; "
       f"linear mu={(p+c)*4.51/10:.15g}; MuRel=1 by normalization")

# Independently reconstruct the generator's >800-keV expressions.
electrons_per_g = 6.02214076e23 * sum(w * z / x.AtomicWeight(z) for z, w in
                                       zip(composition["Elements"], composition["massFractions"]))
slope = math.log(x.CS_Photo_CP("CsI", 800) / x.CS_Photo_CP("CsI", 600)) / math.log(800/600)
record(f"electrons/g={electrons_per_g:.15g}; photo extrapolation exponent={slope:.15g}")
for energy in (1000, 1250, 1500):
    k = energy / 510.99895
    ln = math.log(1 + 2*k)
    kn = 2*math.pi*(2.8179403262e-13)**2 * (
        (1+k)/k**2 * (2*(1+k)/(1+2*k) - ln/k) + ln/(2*k) - (1+3*k)/(1+2*k)**2)
    pe = x.CS_Photo_CP("CsI", 800) * (energy/800)**slope
    model = kn * electrons_per_g + pe
    near(mu[E.index(energy)], model, 1e-14, f"{energy} keV independent analytic extension")
    record(f"{energy} keV: photo={pe:.15g}; Compton={kn*electrons_per_g:.15g}; pf={pe/model:.15g}")
direct = x.CS_Compt_CP("CsI", 800)
extension = g["kn"](800) * electrons_per_g
record(f"800 keV Compton transition: direct={direct:.15g}, KN limit={extension:.15g}, "
       f"relative discontinuity={(extension/direct-1)*100:.9g}% (unmodified)")

# Compare every generated row with C# at its literal rounding precision.
source = (ROOT / "src/Gcam.Detector/CrystalMaterial.cs").read_text(encoding="utf-8")
for material in tables:
    match = re.search(r'new\("' + material["key"] + r'".*?energyKeV: \[(.*?)\],\s*'
                      r'massAttenuation: \[(.*?)\],\s*photoFraction: \[(.*?)\]', source, re.S)
    if match is None:
        check(False, f"{material['key']} C# row must exist")
        continue
    for column, group in zip(("E", "mu", "pf"), match.groups()):
        tokens = [t.strip() for t in group.split(",")]
        check(len(tokens) == len(material[column]), f"{material['key']} {column} length matches generator")
        for token, full in zip(tokens, material[column]):
            # Half the last displayed decimal unit, with machine arithmetic allowance.
            unit = 10 ** (-len(token.split(".")[1])) if "." in token else 1
            assert abs(float(token) - full) <= unit/2 + 1e-12, (material["key"], column, token, full)
    record(f"PASS: {material['key']} C# arrays agree within literal rounding half-units")

record("Result: " + ("STOP: " + "; ".join(failures) if failures else "all generation checks passed"))
record("Existing GAGG bound/tests unchanged. High-energy approximation residuals are reported separately from formula checks.")
(HERE / "CsI_checks.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
print("\n".join(lines))
if failures:
    raise SystemExit("FAILED; implementation must stop: " + "; ".join(failures))
print("CsI C# row:")
print('        new("CsI", "CsI:Tl", "CsI", densityGPerCm3: 4.51,')
for key, field, precision in (("E", "energyKeV", "g"), ("mu", "massAttenuation", ".5g"), ("pf", "photoFraction", ".4g")):
    suffix = "])," if key == "pf" else "],"
    print(f"            {field}: [" + ", ".join(format(v, precision) for v in csi[key]) + suffix)
