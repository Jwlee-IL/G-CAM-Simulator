// Digital pulse-height peak detector for the ADC front-end of the gamma camera.
//
// Each gamma interaction in the crystal produces a positive-going pulse on the
// digitized (ADC) signal. This module extracts, per pulse, its amplitude
// (= energy) and the sample index of its peak:
//   IDLE  : track a slow baseline; when the signal rises THRESH above it, start.
//   TRACK : hold the running maximum until the signal falls back near baseline,
//           then emit (peak_amp = max - baseline, peak_time = sample of the max).
//
// Overlapping pulses (high count rate) merge into one detection -> pile-up loss,
// which is exactly the rate limitation of the real front-end.
module peak_detector #(
    parameter int WIDTH      = 12,   // ADC width (unsigned)
    parameter int THRESH     = 80,   // start: counts above baseline
    parameter int END_THRESH = 40    // end (hysteresis): counts above baseline
)(
    input  logic              clk,
    input  logic              rst_n,
    input  logic              sample_valid,
    input  logic [WIDTH-1:0]  adc,
    output logic              peak_valid,   // 1-cycle strobe
    output logic [WIDTH-1:0]  peak_amp,     // peak - baseline
    output logic [31:0]       peak_time     // sample index of the peak
);
    typedef enum logic {S_IDLE, S_TRACK} state_t;
    state_t state;

    int          baseline;
    int          peak_val;
    logic        seeded;
    logic [31:0] sample_idx;
    logic [31:0] peak_idx;

    always_ff @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            state      <= S_IDLE;
            peak_valid <= 1'b0;
            baseline   <= 0;
            peak_val   <= 0;
            seeded     <= 1'b0;
            sample_idx <= 32'd0;
            peak_idx   <= 32'd0;
            peak_amp   <= '0;
            peak_time  <= 32'd0;
        end else begin
            peak_valid <= 1'b0;
            if (sample_valid) begin
                sample_idx <= sample_idx + 1;
                if (!seeded) begin
                    // Seed the baseline from the first (quiet) sample so we don't
                    // false-trigger while a from-zero baseline creeps up.
                    baseline <= int'(adc);
                    seeded   <= 1'b1;
                end else
                case (state)
                    S_IDLE: begin
                        // Slow baseline tracking (±1 creep toward the idle signal).
                        if (int'(adc) > baseline)      baseline <= baseline + 1;
                        else if (int'(adc) < baseline) baseline <= baseline - 1;

                        if (int'(adc) > baseline + THRESH) begin
                            state    <= S_TRACK;
                            peak_val <= int'(adc);
                            peak_idx <= sample_idx;
                        end
                    end
                    S_TRACK: begin
                        if (int'(adc) > peak_val) begin
                            peak_val <= int'(adc);
                            peak_idx <= sample_idx;
                        end
                        if (int'(adc) < baseline + END_THRESH) begin
                            state      <= S_IDLE;
                            peak_valid <= 1'b1;
                            peak_amp   <= peak_val - baseline;
                            peak_time  <= peak_idx;
                        end
                    end
                    default: state <= S_IDLE;
                endcase
            end
        end
    end
endmodule
