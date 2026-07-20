`timescale 1ns/1ps
// CR-RC^ORDER semi-Gaussian shaper (the classic analogue shaper, in digital form) — a companion to the
// trapezoidal_shaper. Two stages:
//   1. pole-zero deconvolution of the exponential tail:  imp[n] = x[n] - A·x[n-1]   (A = exp(-1/tau), Q16)
//      turns an exp-tail pulse back into a charge impulse.
//   2. ORDER cascaded single-pole RC low-passes:         acc_i += (u - acc_i)·K     (K = 1/tau_s, Q16)
//      each stage feeding the next SAME sample, giving a Gamma-shaped (semi-Gaussian) pulse whose PEAK ∝
//      deposited energy.
// Bit-exact to trap_ref.crrc_int (Python >> = SV signed >>>). Icarus/Yosys friendly: plain parameters,
// `integer` loop vars, arithmetic >>> for the signed Q16 divides.
module crrc_shaper #(
    parameter WIN    = 16,      // signed input sample width
    parameter ORDER  = 4,       // number of RC low-pass stages
    parameter A_Q16  = 53656,   // round(exp(-1/tau)*65536), tau=5 — pole-zero deconvolution (matches C#/Python)
    parameter K_Q16  = 26214,   // 1/tau_s in Q16 (tau_s=2.5) — RC low-pass gain
    parameter Q      = 16,
    parameter WACC   = 40       // accumulator width
) (
    input  logic                   clk,
    input  logic                   rst,
    input  logic                   valid,
    input  logic signed [WIN-1:0]  sample,
    output logic signed [WACC-1:0] shaped     // last RC stage, registered (+1 latency)
);
    logic signed [WACC-1:0] prev;
    logic signed [WACC-1:0] acc  [0:ORDER-1];
    logic signed [WACC-1:0] accn [0:ORDER-1];
    logic signed [WACC-1:0] imp, u;
    integer i;

    // Combinational: deconvolve, then walk the RC cascade (stage i uses stage i-1's NEW value this sample).
    always_comb begin
        imp = sample - ((A_Q16 * prev) >>> Q);
        u = imp;
        for (i = 0; i < ORDER; i = i + 1) begin
            accn[i] = acc[i] + (((u - acc[i]) * K_Q16) >>> Q);
            u = accn[i];
        end
    end

    always_ff @(posedge clk) begin
        if (rst) begin
            for (i = 0; i < ORDER; i = i + 1) acc[i] <= '0;
            prev <= '0; shaped <= '0;
        end else if (valid) begin
            for (i = 0; i < ORDER; i = i + 1) acc[i] <= accn[i];
            prev   <= sample;
            shaped <= u;                       // u = accn[ORDER-1] after the loop
        end
    end
endmodule
