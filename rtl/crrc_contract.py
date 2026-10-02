"""Independent expected math for the legacy fixture and supported Studio CR-RC presets.
T_sum is a simulation convention; it is separate from the DCR noise window.
"""
import math

SCINTILLATORS = {"gagg": 90.0, "nai": 230.0, "lyso": 40.0, "bgo": 300.0}
PREAMPS = {"fast": (100.0, 150.0), "original": (200.0, 320.0), "slow": (500.0, 800.0)}

def fixtures():
    result = {"legacy": dict(order=4, a=round(math.exp(-1 / 5) * 65536),
                             k=round(65536 / 2.5), rise=1.0, tail=5.0)}
    for scint, decay in SCINTILLATORS.items():
        for preamp, (shaping, csp_tail) in PREAMPS.items():
            rise = max(.5, min(decay, csp_tail) / 8)
            tail = max(rise + .5, max(decay, csp_tail) / 8)
            result[f"{preamp}-{scint}"] = dict(order=4, a=round(math.exp(-1 / tail) * 65536),
                                              k=round(65536 * 4 * 8 / shaping), rise=rise, tail=tail)
    return result
