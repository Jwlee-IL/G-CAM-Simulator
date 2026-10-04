"""Snapshot evaluated decay-radiation data (IAEA LiveChart API, ENSDF) for the AB-4 terrestrial source catalog.

python samples/ambient/fetch_source_data.py --out <new folder>

Read-only HTTP GET of public data. Each response is saved byte-for-byte as <nuclide>.csv (gamma rows,
rad_types=g) or <nuclide>-x.csv (X-ray rows, rad_types=x) with a manifest of URL, SHA256, row count and the
extraction date that the API writes into every row. Existing files are never overwritten: a committed snapshot
is immutable, and the catalog builder checks every hash before use.
"""
import argparse
import csv
import hashlib
import io
import json
import sys
import time
import urllib.request
from pathlib import Path

ENDPOINT = "https://nds.iaea.org/relnsd/v1/data?fields=decay_rads&nuclides={nuclide}&rad_types={kind}"
GROUND_STATES = "https://nds.iaea.org/relnsd/v1/data?fields=ground_states&nuclides={nuclide}"

# Every member of the three chains, including the minor branches (At-218, Rn-218, Tl-210, Hg-206, Tl-206).
NUCLIDES = ["40k",
            "238u", "234th", "234pa", "234u", "230th", "226ra", "222rn", "218po", "218at", "218rn",
            "214pb", "214bi", "214po", "210tl", "210pb", "210bi", "210po", "206hg", "206tl",
            "232th", "228ra", "228ac", "228th", "224ra", "220rn", "216po", "216at", "212pb", "212bi", "212po", "208tl"]


def fetch(url):
    request = urllib.request.Request(url, headers={"User-Agent": "gcam-ambient-snapshot/1 (read-only)"})
    for attempt in range(6):
        try:
            with urllib.request.urlopen(request, timeout=120) as response:
                return response.read()
        except OSError:
            if attempt == 5:
                raise
            time.sleep(10 * (attempt + 1))


def url_for(name):
    stem = name[:-4]
    if stem.endswith("-gs"):
        return GROUND_STATES.format(nuclide=stem[:-3])
    if stem.endswith("-x"):
        return ENDPOINT.format(nuclide=stem[:-2], kind="x")
    return ENDPOINT.format(nuclide=stem, kind="g")


def describe(path):
    body = path.read_bytes()
    text = body.decode("utf-8-sig")
    lines = text.splitlines()
    rows = list(csv.DictReader(io.StringIO(text))) if lines and "," in lines[0] else []
    return dict(File=path.name, Url=url_for(path.name), Sha256=hashlib.sha256(body).hexdigest(), Rows=len(rows),
                ExtractionDates=sorted({r.get("Extraction_date", "") for r in rows}),
                NoDataResponse=None if rows else text.strip()[:40])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--nuclides", nargs="*", default=NUCLIDES)
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    for nuclide in args.nuclides:
        for suffix in ("", "-x", "-gs"):
            target = args.out / f"{nuclide}{suffix}.csv"
            if target.exists():
                continue  # immutable snapshot: never refetched or overwritten
            target.write_bytes(fetch(url_for(target.name)))
            print(f"{target.name}: fetched")
            time.sleep(1)
    # The manifest describes every snapshot in the folder, including ones fetched by an earlier run.
    manifest = [describe(p) for p in sorted(args.out.glob("*.csv"))]
    (args.out / "fetch-manifest.json").write_bytes((json.dumps(manifest, indent=1) + "\n").encode("utf-8"))  # LF on every OS: hashed files must not depend on the platform


if __name__ == "__main__":
    main()