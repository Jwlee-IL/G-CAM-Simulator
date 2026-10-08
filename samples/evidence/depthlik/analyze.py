"""Descriptive summaries only; retained approximate LR values are not certified intervals."""
import argparse
import collections
import csv
import math
import statistics as st


def read(path):
    with open(path, encoding='utf-8-sig', newline='') as f:
        return list(csv.DictReader(f))


def wrong(r):
    z, a = float(r['z']), float(r['a_mrad'])
    phi = 1000 * z * math.tan(a / 1000) / (z - 80)
    return (float(r['lambda_truth']) > 25 or float(r['lambda_truth']) < -10
            or math.hypot(float(r['phix_mrad']) - phi, float(r['phiy_mrad'])) > 3)


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('csv')
    ap.add_argument('--v2', help='original v2 timing rows; necessary when v3 switches')
    args = ap.parse_args()
    rows = read(args.csv)
    key = lambda r: tuple(r[k] for k in ('z', 'a_mrad', 'live_s', 'seed', 'channel'))
    if len({key(r) for r in rows}) != len(rows):
        raise SystemExit('duplicate condition/seed/channel')
    v2 = {key(r): r for r in read(args.v2)} if args.v2 else {}
    groups = collections.defaultdict(list)
    for r in rows:
        groups[(int(float(r['live_s'])), r['channel'], int(float(r['z'])), int(float(r['a_mrad'])))].append(r)
    print('| T | channel | z | angle | N | counts | bias | SE | SD | median error | median Hessian sigma | wrong | fallback | cover 68 | cover 95 | mean LR | fit seconds v2+v3 |')
    print('|' + '---|' * 17)
    for (t, ch, z, a), g in sorted(groups.items()):
        good = [r for r in g if not wrong(r)]
        errors = [float(r['zhat']) - z for r in good]
        sigma = [float(r['sigma_z_hessian']) for r in good if math.isfinite(float(r['sigma_z_hessian']))]
        sd = st.stdev(errors) if len(errors) > 1 else math.nan
        times = [float(v2[key(r)]['seconds']) + float(r['v3_seconds']) for r in g] if v2 else []
        values = [t, ch, z, a, len(g), f'{st.mean(float(r["counts"]) for r in g):.0f}',
                  f'{st.mean(errors):.3f}', f'{sd / math.sqrt(len(errors)):.3f}', f'{sd:.3f}',
                  f'{st.median(errors):.3f}', f'{st.median(sigma):.3f}' if sigma else 'NA',
                  sum(wrong(r) for r in g), sum(r['surf_ok'] == '0' for r in g),
                  f'{sum(float(r["lambda_truth"]) <= 1 for r in g)}/{len(g)}',
                  f'{sum(float(r["lambda_truth"]) <= 3.84 for r in g)}/{len(g)}',
                  f'{st.mean(float(r["lambda_truth"]) for r in good):.3f}',
                  f'{st.mean(times):.1f}' if times else 'NA']
        print('| ' + ' | '.join(map(str, values)) + ' |')
    print(f'Rows={len(rows)}; wrong={sum(wrong(r) for r in rows)}; '
          f'negative LR={sum(float(r["lambda_truth"]) < 0 for r in rows)}; '
          f'coverage={sum(float(r["lambda_truth"]) <= 1 for r in rows)}/{len(rows)}, '
          f'{sum(float(r["lambda_truth"]) <= 3.84 for r in rows)}/{len(rows)}; '
          f'switched={sum(r.get("v3_switched") == "1" for r in rows)}')


if __name__ == '__main__':
    main()
