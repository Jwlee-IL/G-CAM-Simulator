"""TODO-19 RD-9: draw the readout schematic ("pseudo-artwork") from the engine-resolved configuration (stdlib only).

    python samples/readout/schematic.py <prefix>          # reads <prefix>.config.json, writes <prefix>.svg

<prefix>.config.json is written by `montecarlo readout-export` from the same request the study runs, after the engine
applied its defaults, so a changed pitch, wall, sensor grid, topology, resistor value, pulse, ADC or trigger redraws
here. The network is drawn in the generic published form of its topology (discretised positioning circuit: row chains
into two column chains; corner grid: a resistor mesh drained at the corners). Illustrative only — not a buildable design,
no component values beyond those the simulation uses. Output is deterministic text (fixed number formats).
"""
import json
import pathlib
import sys

INK, MUTED, FILL, ACCENT, PAPER = '#1f2933', '#7b8794', '#e4e7eb', '#2f6fdd', '#ffffff'


def f(x):
    return f'{x:.1f}'


class Svg:
    def __init__(self, width, height):
        self.w, self.h, self.items = width, height, []

    def add(self, s):
        self.items.append(s)

    def rect(self, x, y, w, h, fill='none', stroke=INK, sw=1.0, rx=0):
        self.add(f'<rect x="{f(x)}" y="{f(y)}" width="{f(w)}" height="{f(h)}" rx="{rx}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}"/>')

    def line(self, x1, y1, x2, y2, stroke=INK, sw=1.0, dash=None):
        d = f' stroke-dasharray="{dash}"' if dash else ''
        self.add(f'<line x1="{f(x1)}" y1="{f(y1)}" x2="{f(x2)}" y2="{f(y2)}" stroke="{stroke}" stroke-width="{sw}"{d}/>')

    def circle(self, x, y, r, fill=INK):
        self.add(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}" fill="{fill}"/>')

    def text(self, x, y, s, size=12, anchor='start', fill=INK, weight='normal'):
        s = s.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')
        self.add(f'<text x="{f(x)}" y="{f(y)}" font-size="{size}" text-anchor="{anchor}" fill="{fill}" font-weight="{weight}">{s}</text>')

    def poly(self, pts, fill='none', stroke=INK, sw=1.0):
        p = ' '.join(f'{f(x)},{f(y)}' for x, y in pts)
        self.add(f'<polygon points="{p}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}"/>')

    def resistor(self, x1, y1, x2, y2, sw=0.8):
        """A resistor symbol (small box) centred on the segment, wires to both ends."""
        mx, my = (x1 + x2) / 2, (y1 + y2) / 2
        horizontal = abs(x2 - x1) >= abs(y2 - y1)
        L = min(abs(x2 - x1) if horizontal else abs(y2 - y1), 14) * 0.6
        t = 3.2
        if horizontal:
            s = 1 if x2 > x1 else -1
            self.line(x1, y1, mx - s * L / 2, my, sw=sw)
            self.line(mx + s * L / 2, my, x2, y2, sw=sw)
            self.rect(mx - L / 2, my - t / 2, L, t, fill=PAPER, sw=sw)
        else:
            s = 1 if y2 > y1 else -1
            self.line(x1, y1, mx, my - s * L / 2, sw=sw)
            self.line(mx, my + s * L / 2, x2, y2, sw=sw)
            self.rect(mx - t / 2, my - L / 2, t, L, fill=PAPER, sw=sw)

    def render(self):
        head = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w}" height="{self.h}" viewBox="0 0 {self.w} {self.h}" '
                f'font-family="Segoe UI, Helvetica, Arial, sans-serif">')
        return '\n'.join([head, f'<rect width="{self.w}" height="{self.h}" fill="{PAPER}"/>'] + self.items + ['</svg>']) + '\n'


def ohm(v):
    return f'{v / 1000:g} kΩ' if v >= 1000 else f'{v:g} Ω'


def draw(cfg):
    c, s, net = cfg['Crystals'], cfg['Sensors'], cfg['Network']
    pulse, adc, trig = cfg['Pulse'], cfg['Digitizer'], cfg['Trigger']
    nx, ny, sx, sy = c['CountX'], c['CountY'], s['CountX'], s['CountY']
    four = cfg['Mode'] == 'FourOutputAnger'
    svg = Svg(1480, 600)
    svg.text(20, 30, f"Readout schematic — {cfg['Readout']} on {cfg['Geometry']} (trigger {cfg['TriggerName']})", 18, weight='bold')
    svg.text(20, 50, 'Illustrative pseudo-artwork generated from the engine-resolved configuration (TODO-19 RD-9); '
                     'generic published network form; not a buildable design.', 12, fill=MUTED)

    # Crystal / SiPM array, drawn to scale of pitch vs wall.
    ax, ay, size = 30, 110, 340
    cell = size / max(nx, ny)
    wall = cell * c['GapMm'] / c['PitchMm']
    svg.text(ax, 76, f"{nx} × {ny} {c['Material']} crystals → SiPM array", 12, weight='bold')
    svg.text(ax, 92, f"{c['PitchMm']:g} mm pitch, {c['GapMm']:g} mm wall, {c['DepthMm']:g} mm deep", 11)
    for iy in range(ny):
        for ix in range(nx):
            x0, y0 = ax + ix * cell, ay + (ny - 1 - iy) * cell
            svg.rect(x0, y0, cell, cell, fill='#9aa5b1', stroke='none')
            svg.rect(x0 + wall / 2, y0 + wall / 2, cell - wall, cell - wall, fill=FILL, stroke=INK, sw=0.4)
    scell = size / max(sx, sy) * s['PitchMm'] / max(c['PitchMm'], s['PitchMm'])
    sact = scell * s['ActiveWidthMm'] / s['PitchMm']
    for ky in range(sy):
        for kx in range(sx):
            x0 = ax + kx * scell + (scell - sact) / 2
            y0 = ay + (sy - 1 - ky) * scell + (scell - sact) / 2
            svg.rect(x0 + sact * 0.2, y0 + sact * 0.2, sact * 0.6, sact * 0.6, fill='none', stroke=ACCENT, sw=0.6)
    matched = 'matched 1:1 (virtual sensor)' if s['MatchedOneToOne'] else 'not 1:1'
    captions = [f"SiPM {sx} × {sy}: {s['ActiveWidthMm']:g} mm active on {s['PitchMm']:g} mm, {matched}",
                f"PDE {s['Pde']:g} (microcell fill included), ENF {s['Enf']:g}",
                f"optics: {cfg['Optics']['Surface'].lower()} wall reflector, R = {cfg['Optics']['WallReflectance']:g}",
                'blue squares = SiPM anodes; +y up; anode k = ky·Sx + kx']
    for i, caption in enumerate(captions):
        svg.text(ax, ay + size + 20 + 16 * i, caption, 11, fill=MUTED if i == 3 else INK)

    # Network.
    nxp, nyp, nw, nh = 480, 140, 380, 300
    svg.text(nxp - 20, 76, f"Charge division: {net['Topology']}", 12, weight='bold')
    outs = {}
    if four and net['Topology'] == 'Dpc':
        left, right = nxp, nxp + nw
        xs = [left + (k + 1) * nw / (sx + 1) for k in range(sx)]
        ys = [nyp + nh - (r + 0.5) * nh / sy for r in range(sy)]
        for r in range(sy):
            y = ys[r]
            pts = [left] + xs + [right]
            for i in range(len(pts) - 1):
                svg.resistor(pts[i], y, pts[i + 1], y)
            for x in xs:
                svg.circle(x, y, 1.8, ACCENT)
        bottom, top = nyp + nh + 22, nyp - 22
        for x, (lo, hi) in ((left, ('A', 'C')), (right, ('B', 'D'))):
            col = [bottom] + ys + [top]
            for i in range(len(col) - 1):
                svg.resistor(x, col[i], x, col[i + 1])
            for y in ys:
                svg.circle(x, y, 2.2)
            outs[lo], outs[hi] = (x, bottom), (x, top)
        svg.text(nxp + nw / 2, nyp + nh + 66, f"row chains R_row = {ohm(net['RowResistanceOhm'])}, column chains R_col = "
                                             f"{ohm(net['ColumnResistanceOhm'])} (ratio {net['ColumnResistanceOhm'] / net['RowResistanceOhm']:g})",
                 11, 'middle')
    elif four and net['Topology'] == 'CornerGrid':
        xs = [nxp + (k + 0.5) * nw / sx for k in range(sx)]
        ys = [nyp + nh - (r + 0.5) * nh / sy for r in range(sy)]
        for r in range(sy):
            for k in range(sx):
                if k + 1 < sx:
                    svg.resistor(xs[k], ys[r], xs[k + 1], ys[r], sw=0.5)
                if r + 1 < sy:
                    svg.resistor(xs[k], ys[r], xs[k], ys[r + 1], sw=0.5)
                svg.circle(xs[k], ys[r], 1.6, ACCENT)
        corner = {'A': (0, 0), 'B': (sx - 1, 0), 'C': (0, sy - 1), 'D': (sx - 1, sy - 1)}
        for name, (k, r) in corner.items():
            x, y = xs[k], ys[r]
            ty = y + 22 if r == 0 else y - 22
            svg.resistor(x, y, x, ty)
            outs[name] = (x, ty)
        svg.text(nxp + nw / 2, nyp + nh + 66, f"mesh R = {ohm(net['GridResistanceOhm'])}, corner drains "
                                             f"{ohm(net['DrainResistanceOhm'])}", 11, 'middle')
    elif four:
        svg.rect(nxp, nyp, nw, nh, fill=FILL)
        svg.text(nxp + nw / 2, nyp + nh / 2, 'ideal bilinear divider (weights, not a circuit)', 12, 'middle')
        for name, (x, y) in {'A': (nxp, nyp + nh), 'B': (nxp + nw, nyp + nh), 'C': (nxp, nyp), 'D': (nxp + nw, nyp)}.items():
            outs[name] = (x, y)
    else:
        svg.rect(nxp, nyp, nw, nh, fill=FILL)
        svg.text(nxp + nw / 2, nyp + nh / 2, f'no division: one channel per SiPM ({sx * sy} channels)', 12, 'middle')
    svg.line(ax + size + 5, ay + size / 2, nxp - 25, ay + size / 2, stroke=ACCENT, sw=2)
    svg.poly([(nxp - 25, ay + size / 2 - 5), (nxp - 15, ay + size / 2), (nxp - 25, ay + size / 2 + 5)], fill=ACCENT, stroke=ACCENT)
    svg.text((ax + size + nxp) / 2 - 10, ay + size / 2 - 8, f'{sx * sy} anodes', 11, 'middle', ACCENT)
    load = 'virtual-ground inputs (R_in = 0)' if net['InputImpedanceOhm'] == 0 else f"inputs R_in = {ohm(net['InputImpedanceOhm'])}"
    if four:
        svg.text(nxp + nw / 2, nyp + nh + 82, f'outputs A = x−y−, B = x+y−, C = x−y+, D = x+y+; {load}', 11, 'middle')

    # Four channels: preamp/shaper → ADC → FPGA.
    chx, fpx = 960, 1250
    rows = {'D': 150, 'C': 230, 'B': 310, 'A': 390} if four else {}
    rails = {'D': nyp - 52, 'C': nyp - 44, 'B': nyp + nh + 44, 'A': nyp + nh + 52}
    bends = {'D': chx - 38, 'C': chx - 46, 'B': chx - 46, 'A': chx - 38}
    for name, y in rows.items():
        ox, oy = outs[name]
        # Route along a rail above (C, D) or below (A, B) the network, then down / up to the channel.
        rail, bx = rails[name], bends[name]
        for (x1, y1, x2, y2) in ((ox, oy, ox, rail), (ox, rail, bx, rail), (bx, rail, bx, y), (bx, y, chx - 20, y)):
            svg.line(x1, y1, x2, y2, stroke=MUTED, sw=0.9, dash='4 3')
        svg.circle(ox, oy, 3)
        svg.text(ox + (8 if ox > nxp + nw / 2 else -8), oy + 4, name, 13, 'start' if ox > nxp + nw / 2 else 'end', weight='bold')
        svg.poly([(chx - 20, y - 18), (chx - 20, y + 18), (chx + 14, y)], fill=PAPER)
        svg.text(chx - 12, y + 4, name, 11, weight='bold')
        svg.rect(chx + 26, y - 20, 108, 40, fill=FILL)
        svg.text(chx + 80, y - 4, 'preamp + shaper', 11, 'middle')
        svg.text(chx + 80, y + 11, f"τr {pulse['RiseNs']:g} / τt {pulse['TailNs']:g} ns", 10, 'middle', MUTED)
        svg.line(chx + 14, y, chx + 26, y)
        svg.rect(chx + 150, y - 20, 84, 40, fill=FILL)
        svg.text(chx + 192, y - 4, f"ADC {adc['Bits']}-bit", 11, 'middle')
        svg.text(chx + 192, y + 11, f"{adc['SampleRateMsps']:g} MSPS", 10, 'middle', MUTED)
        svg.line(chx + 134, y, chx + 150, y)
        svg.line(chx + 234, y, fpx, y)
    if four:
        svg.text(chx - 20, 76, f"Four channels A–D → {adc['Bits']}-bit ADCs", 12, weight='bold')
        svg.text(chx - 20, 92, f"ENOB {adc['Enob']:g}, {adc['FullScaleKeV']:g} keV full scale, noise {adc['NoiseKeV']:g} keV", 11)
    fy, fh = 110, 330
    svg.rect(fpx, fy, 210, fh, fill='#f5f7fa', stroke=ACCENT, sw=1.5, rx=6)
    svg.text(fpx + 105, fy + 22, 'FPGA', 14, 'middle', ACCENT, 'bold')
    unit = 'keV' if trig['Unit'] == 'KeVEquivalent' else 'codes'
    lines = [
        ('Σ = A + B + C + D', True),
        (f"trigger: {trig['Logic'].lower()} ≥ {trig['Threshold']:g} {unit}", False),
        (f"hold: {pulse['Hold']}", False),
        (f"window {pulse['HoldWindowNs']:g} ns, dead {pulse['DeadTimeNs']:g} ns", False),
        ('Anger ratio', True),
        ('X = (B + D − A − C) / Σ', False),
        ('Y = (C + D − A − B) / Σ', False),
        ('flood-map LUT', True),
        (f"{cfg['Segmentation'].lower()} → crystal ID", False),
        ('E = Σ × gain[crystal]', False),
        ('→ list-mode event', True),
    ]
    y = fy + 50
    for text, bold in lines:
        svg.text(fpx + 14, y, text, 11.5, weight='bold' if bold else 'normal')
        y += 27 if bold else 22
    svg.text(20, 585, 'Every label above comes from the configuration the simulation uses; regenerate with '
                      '`montecarlo readout-export` + `python samples/readout/schematic.py` after changing pitch, wall, topology '
                      'or front-end values.', 11, fill=MUTED)
    return svg.render()


def main():
    if len(sys.argv) != 2:
        print(__doc__)
        return 1
    prefix = sys.argv[1]
    cfg = json.loads(pathlib.Path(prefix + '.config.json').read_text(encoding='utf-8'))
    pathlib.Path(prefix + '.svg').write_bytes(draw(cfg).encode('utf-8'))
    print(f'wrote {prefix}.svg')
    return 0


if __name__ == '__main__':
    sys.exit(main())
