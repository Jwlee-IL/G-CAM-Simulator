"""AB-4 / AB-4d terrestrial source catalog: K-40, U-238 series and Th-232 series in secular equilibrium.

python samples/ambient/build_catalog.py --out samples/ambient/terrestrial-source-catalog-v1.json

Source term rule (Decision AB-4d): only photon lines (gamma and X-ray) with evaluated absolute intensities.
Every listed transition without a photon intensity is written under NotIncluded with its reason; no substitute
intensity is ever assigned. The AB-4c omission bounds of turn 5 stay on record unchanged.

Data: IAEA LiveChart of Nuclides API (ENSDF decay radiations and ground-state decay modes), immutable snapshots in
source-data/v1, each checked against fetch-manifest.json before use. LiveChart decay-radiation intensities are
absolute per 100 decays of the parent state (all modes): e.g. Bi-212 727.33 keV 6.67 % is listed on the beta
branch (64.06 %) and Tl-208 2614.511 keV 99.754 % per Tl-208 decay; turn 5 checked Bi-214 609 keV against the ENSDF
normalisation. Photon yield per chain decay = occurrence of the parent state per chain decay x intensity / 100.

Pa-234 normalisation (current evaluation, S. Ota, Nuclear Data Sheets 207, 351 (2026), literature cutoff
2023-12-01): Th-234 beta- decay feeds the 73.920-keV (3+) level of Pa-234 directly with I(beta)=0.150(17) %
(which then decays to the Pa-234 ground state); every other Th-234 decay reaches the 1.159-min (0-) isomer, whose
decay is %IT=0.16(4), %beta-=99.84(4). Hence per chain decay: n(Pa-234m) = 1 - 0.00150 and
n(Pa-234 g.s.) = 0.00150 + n(Pa-234m) * 0.0016. The LiveChart line data for A=234 are the 2006 evaluation
(Browne and Tuli), whose isomer energy label is 73.92 keV (now 76.5 keV); only its line intensities are used.
"""
import argparse
import csv
import hashlib
import io
import json
from pathlib import Path

HERE = Path(__file__).parent
SOURCE = HERE / "source-data" / "v1"
MIN_TRANSPORT_KEV = 1.0  # lower limit of the XCOM element grid used by the transport

PA234_DIRECT_73 = 0.00150   # Ota 2026, Th-234 beta- decay, I(beta) to the 73.920 level, 0.150(17) %
PA234M_IT = 0.0016          # Ota 2026, adopted levels, 76.5-keV isomer %IT = 0.16(4)
PA234M_BETA = 0.9984        # Ota 2026, %beta- = 99.84(4)

# (daughter, parent, LiveChart decay mode of the parent). Order is topological. The evaluated Po-216 record has no
# beta- branch (alpha 100 %), so At-216 is not a chain member; its snapshot is kept only as the record of that check.
U_EDGES = [("234th", "238u", "A"), ("234pam", "234th", "B-"), ("234pa", "234th", None), ("234pa", "234pam", "IT"),
           ("234u", "234pam", "B-"), ("234u", "234pa", "B-"), ("230th", "234u", "A"), ("226ra", "230th", "A"),
           ("222rn", "226ra", "A"), ("218po", "222rn", "A"), ("214pb", "218po", "A"), ("218at", "218po", "B-"),
           ("218rn", "218at", "B-"), ("214bi", "214pb", "B-"), ("214bi", "218at", "A"), ("214po", "214bi", "B-"),
           ("214po", "218rn", "A"), ("210tl", "214bi", "A"), ("210pb", "214po", "A"), ("210pb", "210tl", "B-"),
           ("210bi", "210pb", "B-"), ("206hg", "210pb", "A"), ("210po", "210bi", "B-"), ("206tl", "210bi", "A"),
           ("206tl", "206hg", "B-")]
TH_EDGES = [("228ra", "232th", "A"), ("228ac", "228ra", "B-"), ("228th", "228ac", "B-"), ("224ra", "228th", "A"),
            ("220rn", "224ra", "A"), ("216po", "220rn", "A"), ("212pb", "216po", "A"),
            ("212bi", "212pb", "B-"), ("212po", "212bi", "B-"), ("208tl", "212bi", "A")]
CHAINS = {"K-40": ("40k", []), "U-238 series": ("238u", U_EDGES), "Th-232 series": ("232th", TH_EDGES)}


def load_manifest():
    manifest = json.loads((SOURCE / "fetch-manifest.json").read_text(encoding="utf-8"))
    for entry in manifest:
        if hashlib.sha256((SOURCE / entry["File"]).read_bytes()).hexdigest() != entry["Sha256"]:
            raise ValueError(f"{entry['File']}: snapshot hash differs from fetch-manifest.json")
    return {e["File"]: e for e in manifest}


def rows(name):
    text = (SOURCE / name).read_text(encoding="utf-8-sig")
    first = text.splitlines()[0] if text.strip() else ""
    return list(csv.DictReader(io.StringIO(text))) if "," in first else []


def branch(nuclide, mode):
    states = rows(f"{nuclide}-gs.csv")
    if len(states) != 1:
        raise ValueError(f"{nuclide}: expected one ground-state row")
    s = states[0]
    for k in (1, 2, 3):
        if s[f"decay_{k}"] == mode:
            return float(s[f"decay_{k}_%"]) / 100
    raise ValueError(f"{nuclide}: decay mode {mode} not in the evaluated ground-state record")


def occurrences(top, edges):
    n = {top: 1.0}
    formulas = {top: "1"}
    for daughter, parent, mode in edges:
        if parent == "234th" and daughter == "234pam":
            f, text = n[parent] * (1 - PA234_DIRECT_73), "n(Th-234) * (1 - 0.00150)"
        elif parent == "234th" and daughter == "234pa":
            f, text = n[parent] * PA234_DIRECT_73, "n(Th-234) * 0.00150"
        elif parent == "234pam":
            b = PA234M_IT if mode == "IT" else PA234M_BETA
            f, text = n[parent] * b, f"n(Pa-234m) * {b}"
        else:
            b = branch(parent, mode)
            f, text = n[parent] * b, f"n({parent}) * b({parent},{mode})={b}"
        n[daughter] = n.get(daughter, 0.0) + f
        formulas[daughter] = (formulas[daughter] + " + " if daughter in formulas else "") + text
    return n, formulas


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    manifest = load_manifest()
    omissions = json.loads((HERE.parent / "evidence" / "results" / "ambient-baseline-v1-turn5-omissions.json")
                           .read_text(encoding="utf-8"))
    chains = []
    for chain_name, (top, edges) in CHAINS.items():
        n, formulas = occurrences(top, edges)
        lines, not_included, excluded_states, members = [], [], [], []
        for member, occurrence in n.items():
            nuclide = "234pa" if member == "234pam" else member
            parent_level = "73.92" if member == "234pam" else "0"
            g, x = rows(f"{nuclide}.csv"), rows(f"{nuclide}-x.csv")
            members.append(dict(Member=member, ParentLevelKeVInSnapshot=float(parent_level), Occurrence=occurrence,
                                Formula=formulas[member], GammaRows=len(g), Evaluation=sorted({
                                    f"{r['ensdf_authors'].strip()} (cutoff {r['ensdf_publication_cut-off']})" for r in g})))
            xkeys = {(r["energy"], r["intensity"], r["p_energy"], r["decay"]): r for r in x}
            gkeys = {(r["energy"], r["intensity"], r["p_energy"], r["decay"]) for r in g}
            if missing := [k for k in xkeys if k not in gkeys]:
                raise ValueError(f"{nuclide}: X-ray rows absent from the photon file: {missing}")
            for r in g:
                if r["p_energy"] != parent_level:
                    continue
                key = (r["energy"], r["intensity"], r["p_energy"], r["decay"])
                xray = xkeys.get(key)
                kind = "X-ray " + xray["shell"] if xray else "gamma"
                energy = float(r["energy"])
                entry = dict(Nuclide=member, EnergyKeV=energy, Kind=kind, Decay=r["decay"])
                if not r["intensity"]:
                    not_included.append(dict(entry, Reason="Evaluated record lists the transition without a photon intensity (AB-4d: not included, no substitute)."))
                    continue
                if xray and xray["shell"] == "KB":
                    texts = [v["intensity"] for v in x if v["p_energy"] == r["p_energy"] and v["decay"] == r["decay"]
                             and v["shell"] in ("KpB1", "KpB2")]
                    # Rounding allowance only: half a unit in the last printed digit of each of the three values.
                    def half_unit(t):
                        return 0.5 * 10 ** -(len(t.split(".")[1]) if "." in t else 0)
                    allowance = half_unit(r["intensity"]) + sum(half_unit(t) for t in texts)
                    if abs(sum(float(t) for t in texts) - float(r["intensity"])) > allowance:
                        raise ValueError(f"{nuclide}: KB row is not the sum of its K'beta components")
                    not_included.append(dict(entry, Reason="Sum row of the K'beta1 + K'beta2 components, which are included; omitted to avoid double counting."))
                    continue
                if energy < MIN_TRANSPORT_KEV:
                    not_included.append(dict(entry, IntensityPercent=float(r["intensity"]),
                                             Reason="Below the 1 keV lower limit of the XCOM transport data."))
                    continue
                lines.append(dict(entry, IntensityPercentPerParentDecay=float(r["intensity"]),
                                  YieldPerChainDecay=occurrence * float(r["intensity"]) / 100))
            others = {}
            for r in g:
                if r["p_energy"] != parent_level and not (member == "234pa" and r["p_energy"] == "73.92"):
                    others.setdefault((r["p_energy"], r["decay"]), 0)
                    others[(r["p_energy"], r["decay"])] += 1
            if member != "234pam":
                excluded_states += [dict(Nuclide=member, ParentLevelKeV=float(k[0]), Decay=k[1], Rows=c,
                                         Reason="Parent state not populated in the decay chain.") for k, c in others.items()]
            if not g:
                not_included.append(dict(Nuclide=member, EnergyKeV=None, Kind="all", Decay="all",
                                         Reason="No evaluated photon-emission data in the snapshot (LiveChart no-data response)."))
        lines.sort(key=lambda l: (l["EnergyKeV"], l["Nuclide"]))
        chain_omissions = [o for o in omissions["SourceOmissions"] + omissions["StoppedOmissions"]] if chain_name == "U-238 series" else []
        chains.append(dict(Name=chain_name, Top=top, Members=members, Lines=lines,
                           PhotonsPerDecay=sum(l["YieldPerChainDecay"] for l in lines),
                           PhotonEnergyKeVPerDecay=sum(l["YieldPerChainDecay"] * l["EnergyKeV"] for l in lines),
                           NotIncluded=not_included, ExcludedParentStates=excluded_states,
                           Ab4cOmissionsOnRecord=chain_omissions))
    out = dict(SchemaVersion=1, Id="terrestrial-source-catalog-v1", Version=1, Date="2026-10-04",
               Rule="AB-4d: evaluated photon lines with absolute intensities only; transitions without intensity listed under NotIncluded.",
               Activities=dict(Reference="UNSCEAR 2000 Report, Vol. I, Annex B, Table 6, population-weighted soil concentrations",
                               BqPerKg={"K-40": 420, "U-238 series": 33, "Th-232 series": 45}),
               Pa234Normalisation=dict(Reference="S. Ota, Nuclear Data Sheets 207, 351 (2026): https://www.nndc.bnl.gov/ensnds/234/Pa/adopted.pdf and https://www.nndc.bnl.gov/ensnds/234/Pa/beta_decay.pdf",
                                       DirectFeedingOf73920Level=PA234_DIRECT_73, IsomerIT=PA234M_IT, IsomerBeta=PA234M_BETA,
                                       Note="The evaluation lists both the 0.150 % beta feeding of the 73.920 level (deduced from intensity balance) and %IT=0.16 of the isomer, whose IT path (unobserved 2.6-keV transition) also ends at the 73.920 level. Both are used as published; if the observed 73.92-keV de-excitation were wholly the IT path, n(Pa-234 g.s.) would be 0.0016 instead of 0.0031 (effect quoted in the turn-6 report)."),
               SourceSnapshots=dict(Folder="samples/ambient/source-data/v1", Manifest="fetch-manifest.json",
                                    ManifestSha256=hashlib.sha256((SOURCE / "fetch-manifest.json").read_bytes()).hexdigest(),
                                    Api="https://nds.iaea.org/relnsd/vcharthtml/api_v0_guide.html"),
               Ab4cOmissionsSource="samples/evidence/results/ambient-baseline-v1-turn5-omissions.json (turn 5, unchanged)",
               Chains=chains)
    args.out.write_bytes((json.dumps(out, indent=1) + "\n").encode("utf-8"))  # LF on every OS: hashed files must not depend on the platform
    for c in chains:
        print(f"{c['Name']}: {len(c['Lines'])} lines, {c['PhotonsPerDecay']:.6f} photons/decay, "
              f"{c['PhotonEnergyKeVPerDecay']:.3f} keV/decay, {len(c['NotIncluded'])} not included")


if __name__ == "__main__":
    main()
