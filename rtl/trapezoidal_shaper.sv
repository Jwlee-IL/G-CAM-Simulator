`timescale 1ns/1ps
// Jordanov-Knoll recursive trapezoidal shaper with pole-zero (decay) correction.
//
// Digital synthesis of a trapezoidal pulse (Jordanov & Knoll, NIM A 345 (1994) 337):
//   d^{k,l}[n] = v[n] - v[n-RISE] - v[n-L] + v[n-RISE-L]      (L = RISE + FLAT)
//   p[n]       = p[n-1] + d^{k,l}[n]
//   r[n]       = p[n]   + M * d^{k,l}[n]                       (M deconvolves the exp tail)
//   s[n]       = s[n-1] + r[n]                                 (trapezoidal output)
//
// For an exponential input (scintillation decay tau), the M term is the pole-zero
// cancellation: M = 1/(exp(Ts/tau) - 1) ~ tau/Ts. It flattens the tail so s[n] is a clean
// trapezoid whose FLAT TOP height ∝ deposited energy — immune to ballistic deficit and to
// pile-up landing outside the flat top. M is passed in Q8 fixed point (round(M*256)).
//
// Icarus-friendly: no `automatic`, no `void'()`; combinational recurrence in always_comb,
// state in always_ff, explicit delay-line shift.
// NOTE: plain (untyped) parameters + `integer` loop vars so BOTH the modern cocotb/Icarus
// flow and the older Yosys 0.9 synthesis frontend parse it (Yosys 0.9 rejects `parameter int`).
module trapezoidal_shaper #(
    parameter WIN   = 16,     // signed input sample width
    parameter RISE  = 10,     // ramp length (samples)
    parameter FLAT  = 8,      // flat-top length (samples)
    parameter M_Q8  = 1156,   // decay deconvolution constant in Q8 (= round(tau/Ts * 256))
    parameter WACC  = 32      // accumulator width
) (
    input  logic                   clk,
    input  logic                   rst,
    input  logic                   valid,       // 1 = `sample` is a new ADC sample this cycle
    input  logic signed [WIN-1:0]  sample,
    output logic signed [WACC-1:0] shaped       // s[n], registered (1-cycle latency)
);
    localparam L  = RISE + FLAT;                // v[n-L] tap
    localparam KL = RISE + L;                   // v[n-RISE-L] tap = longest delay

    logic signed [WIN-1:0]  dl [0:KL-1];        // delay line: dl[i] = v[n-1-i]
    logic signed [WACC-1:0] p, s;
    integer i;

    // Combinational recurrence for the current sample v[n] = `sample`.
    logic signed [WACC-1:0] dkl, p_next, r;
    always_comb begin
        dkl    = sample - dl[RISE-1] - dl[L-1] + dl[KL-1];
        p_next = p + dkl;
        r      = p_next + ((dkl * M_Q8) >>> 8);   // signed arithmetic shift = /256
    end

    always_ff @(posedge clk) begin
        if (rst) begin
            for (i = 0; i < KL; i = i + 1) dl[i] <= '0;
            p <= '0; s <= '0; shaped <= '0;
        end else if (valid) begin
            p      <= p_next;
            s      <= s + r;
            shaped <= s + r;                      // s[n]
            for (i = KL-1; i > 0; i = i - 1) dl[i] <= dl[i-1];
            dl[0] <= sample;
        end
    end
endmodule
