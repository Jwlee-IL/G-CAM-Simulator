`timescale 1ns/1ps
// Gated baseline restorer (BLR) for the trapezoidal shaper.
//
// The shaper's Q8-quantized pole-zero constant (M_Q8 = round(M*256)) cancels the exponential tail only
// approximately, so every pulse leaves a tiny residual step and the DC baseline WALKS over a long pulse
// train (the MC-stream study measured ~-47000 ADC over 1500 events). That makes the ABSOLUTE output
// unusable — the flat-top height has to be read relative to the local pre-pulse level.
//
// This BLR removes the walk: a leaky-integrator estimate of the baseline that updates ONLY in quiet
// regions (|x - base| < GATE, so flat tops never drag it down) and is subtracted from the stream. The
// output is then a stable zero baseline with pulse heights sitting on top of it — a standard technique in
// real trapezoidal DAQs. The estimate is carried in Q(FRAC) fixed point so the small per-sample leak does
// not round to zero; the loop time constant is ~2^FRAC samples (>> a pulse, << the whole train).
//
// Design caveats (inherent to a gated BLR, not bugs): GATE must sit ABOVE the quiet noise/walk excursion
// but BELOW the smallest pulse height you need to keep (a pulse under GATE is read as baseline and partly
// removed); a pulse train so dense that the gate almost never opens leaves no quiet samples to track; and a
// large DC offset already outside GATE at start-up would freeze acquisition (here the stream starts at 0, so
// it acquires fine). For the MC stream (500 kcps, ~85% quiet, flat tops ~146000 >> GATE) all hold.
//
// Icarus/Yosys friendly: plain parameters, `integer`-free, arithmetic >>> for the signed divide.
module baseline_restorer #(
    parameter WACC = 32,     // sample width (matches the shaper accumulator)
    parameter GATE = 4096,   // |x - base| below this = baseline region; above it the estimate freezes
    parameter FRAC = 12      // baseline estimate is Q(FRAC); loop time constant ~ 2^FRAC samples
) (
    input  logic                    clk,
    input  logic                    rst,
    input  logic                    valid,   // 1 = `x` is a new shaped sample this cycle
    input  logic signed [WACC-1:0]  x,       // raw shaped sample (walking baseline)
    output logic signed [WACC-1:0]  y        // baseline-restored sample (registered, +1 latency)
);
    logic signed [WACC+FRAC-1:0] base_acc;   // baseline estimate in Q(FRAC)
    logic signed [WACC-1:0]      base;
    logic signed [WACC-1:0]      diff;

    always_comb begin
        base = base_acc >>> FRAC;            // arithmetic shift = / 2^FRAC (drop the fractional bits)
        diff = x - base;
    end

    always_ff @(posedge clk) begin
        if (rst) begin
            base_acc <= '0;
            y        <= '0;
        end else if (valid) begin
            // Integrate toward the current sample ONLY when it looks like baseline (small excursion);
            // a flat top (large |diff|) freezes the estimate so the pulse height is preserved.
            if (diff < GATE && diff > -GATE)
                base_acc <= base_acc + diff;
            y <= x - base;                   // subtract the current baseline estimate
        end
    end
endmodule
