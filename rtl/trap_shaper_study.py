"""Trapezoidal shaper — what it buys, using the RTL-validated integer model (trap_ref, which
is bit-exact to trapezoidal_shaper.sv; the cocotb test in test_trap_shaper.py proves it).

Three points:
  (1) the trapezoid's FLAT TOP measures energy independent of ballistic deficit;
  (2) the pole-zero (M) term deconvolves the exponential tail so the output returns to
      baseline — without it the shaper droops/rides up at rate;
  (3) two piled-up pulses give two resolvable flat tops (each energy recoverable) where a
      peak-hold would merge them into one fake sum.

Usage (from rtl/):  python trap_shaper_study.py
"""
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import trap_ref as tr

fig, (ax1, ax2, ax3) = plt.subplots(1, 3, figsize=(16, 4.8))

# (1) single pulse -> trapezoid with a flat top
N = 120
inp = tr.exp_pulse(N, 20, 662.0)
out = tr.trap_shape(inp)
t = np.arange(N)
ax1.plot(t, np.array(inp) / max(inp), color="#95a5a6", lw=1.5, label="input (scintillation, τ=5)")
o = np.array(out, float); o /= o.max()
ax1.plot(t, o, color="#2e8b57", lw=2, label="trapezoidal output")
ax1.axhline(o.max(), ls=":", color="#2e8b57")
ax1.text(70, o.max() - 0.12, "flat top = energy\n(no ballistic deficit)", fontsize=8, color="#2e8b57")
ax1.set_title(f"(1) Recursive trapezoid (RISE {tr.RISE}, FLAT {tr.FLAT})\nflat top ∝ deposited energy")
ax1.set_xlabel("sample"); ax1.set_ylabel("normalized"); ax1.legend(fontsize=8); ax1.grid(alpha=0.3)

# (2) pole-zero: correct M vs M=0 (no deconvolution)
out_pz = tr.trap_shape(inp, m_q8=tr.M_Q8)
out_no = tr.trap_shape(inp, m_q8=0)
ax2.plot(t, np.array(out_pz) / max(out_pz), color="#2e8b57", lw=2, label=f"pole-zero (M={tr.M:.1f})")
ax2.plot(t, np.array(out_no) / max(out_pz), color="#c0392b", lw=2, ls="--", label="no deconvolution (M=0)")
ax2.axhline(0, color="#888", lw=0.8)
ax2.text(58, 0.55, "M=0: rounded top, no flat\n(ballistic deficit) + longer tail\n→ baseline buildup at rate", fontsize=8, color="#c0392b")
ax2.set_title("(2) Pole-zero (M) deconvolves the decay tail\n→ output returns to baseline")
ax2.set_xlabel("sample"); ax2.set_ylabel("normalized"); ax2.legend(fontsize=8); ax2.grid(alpha=0.3)

# (3) two piled-up pulses -> two resolvable flat tops
M2 = 170
inp2 = tr.exp_pulse(M2, 20, 662.0)
tr.add_pulse(inp2, 55, 1332.0)
out2 = tr.trap_shape(inp2)
t2 = np.arange(M2)
ax3.plot(t2, np.array(inp2) / max(inp2), color="#95a5a6", lw=1.3, label="piled input (662 + 1332)")
o2 = np.array(out2, float); o2n = o2 / o2.max()
ax3.plot(t2, o2n, color="#2e8b57", lw=2, label="trapezoidal output")
# mark the two flat-top levels
lvl1 = np.max(o2[30:48]); lvl2 = np.max(o2[65:90])
ax3.annotate("flat top 1\n(662)", (40, lvl1 / o2.max()), (18, 0.35), fontsize=8, color="#2e8b57",
             arrowprops=dict(arrowstyle="->", color="#2e8b57"))
ax3.annotate("flat top 2\n(1332)", (78, lvl2 / o2.max()), (95, 0.6), fontsize=8, color="#2e8b57",
             arrowprops=dict(arrowstyle="->", color="#2e8b57"))
ax3.set_title("(3) Pile-up: two resolvable flat tops\n(peak-hold would merge them into a fake sum)")
ax3.set_xlabel("sample"); ax3.set_ylabel("normalized"); ax3.legend(fontsize=8); ax3.grid(alpha=0.3)

fig.suptitle("Trapezoidal shaper (Jordanov-Knoll) in RTL — validated bit-exact against trapezoidal_shaper.sv via cocotb: "
             "flat-top energy (no ballistic deficit), pole-zero baseline restoration, pile-up separation.", fontsize=10)
fig.tight_layout(rect=[0, 0, 1, 0.94])
fig.savefig("trap_shaper.png", dpi=130)
print("saved trap_shaper.png")
