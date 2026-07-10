`timescale 1ns/1ps
// Pipelined trapezoidal shaper — same output as trapezoidal_shaper.sv (BIT-EXACT, +3 samples
// latency), but every feedback loop is a single adder so it closes timing at a much higher clock.
//
// Reformulation: the original s[n] = Σ r[k] = Σ p[k] + Σ m[k] = q[n] + mm[n], where
//   p[n]  = p[n-1]  + dkl[n]          (accumulator 1)
//   q[n]  = q[n-1]  + p[n-1]          (accumulator 2, integrates p)
//   m[n]  = (dkl[n] * M_Q8) >>> 8     (feed-forward, per-sample deconvolution term)
//   mm[n] = mm[n-1] + m[n-1]          (accumulator 3, integrates m)
//   s[n]  = q[n] + mm[n]              (feed-forward combine — NOT in any loop)
// so `q + mm` reproduces the same integer result as the original per-sample `Σ(p + m)`.
// The only feedback loops are the three `x += y` accumulators (1 adder each); the FIR `dkl`,
// the constant multiply `m`, and the output combine are all feed-forward register stages.
// Verified bit-exact (latency 3) against trap_ref.trap_shape by the cocotb test.
module trapezoidal_shaper_pl #(
    parameter WIN  = 16,
    parameter RISE = 10,
    parameter FLAT = 8,
    parameter M_Q8 = 1156,
    parameter WACC = 32
) (
    input  logic                   clk,
    input  logic                   rst,
    input  logic                   valid,
    input  logic signed [WIN-1:0]  sample,
    output logic signed [WACC-1:0] shaped
);
    localparam L  = RISE + FLAT;
    localparam KL = RISE + L;

    logic signed [WIN-1:0]  dl [0:KL-1];
    logic signed [WACC-1:0] dkl_r, p, q, m_r, mm;   // dkl_r/m_r = feed-forward stage regs; p/q/mm = accumulators
    integer i;

    // FIR: dkl_c is WACC-wide, so the 4-tap sum is evaluated at WACC width (context-determined
    // sizing per IEEE 1800) and the WIN-wide signed taps sign-extend — no WIN-width overflow.
    logic signed [WACC-1:0] dkl_c;
    always_comb dkl_c = sample - dl[RISE-1] - dl[L-1] + dl[KL-1];

    always_ff @(posedge clk) begin
        if (rst) begin
            for (i = 0; i < KL; i = i + 1) dl[i] <= '0;
            dkl_r <= '0; p <= '0; q <= '0; m_r <= '0; mm <= '0; shaped <= '0;
        end else if (valid) begin
            dkl_r  <= dkl_c;                          // stage: register the FIR
            p      <= p  + dkl_r;                     // accumulator 1 (1 adder)
            m_r    <= (dkl_r * M_Q8) >>> 8;           // stage: register the constant multiply
            q      <= q  + p;                         // accumulator 2 (1 adder)
            mm     <= mm + m_r;                       // accumulator 3 (1 adder)
            shaped <= q  + mm;                        // output combine (feed-forward, 1 adder)
            for (i = KL-1; i > 0; i = i - 1) dl[i] <= dl[i-1];
            dl[0] <= sample;
        end
    end
endmodule
