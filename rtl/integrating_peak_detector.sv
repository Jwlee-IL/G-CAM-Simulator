// Improved front-end: gated charge-integration peak detector WITH pile-up rejection.
//
// The baseline peak_detector.sv measures every pulse's peak amplitude and emits it
// blindly -> at rate, overlapping pulses merge and it reports a contaminated energy
// (see ballistic_deficit_study.py). Charge simply ADDS when pulses overlap, so a naive
// integrator is even more prone to fake "sum" energies than a peak-hold -- unless the
// pile-up is detected and REJECTED. This module:
//   * integrates the CHARGE over a fixed window after each trigger (charge = light =
//     energy; a fixed window >= collection time has no ballistic deficit), and
//   * runs a fast DERIVATIVE arrival detector (slope over DERIV_D samples): a second pulse
//     riding on the tail of the first still shows a fast rise, so it is caught even when the
//     signal never dips back below threshold (the case a slow crystal hides from a simple
//     level/edge detector). Any event with a 2nd arrival inside the window -- or whose
//     signal has not returned to baseline by the window end (tail pile-up) -- is reject=1.
//
// The integration `window` is a runtime input (= the "shaping time") so a study can sweep
// it without recompiling: long window -> full charge / best resolution but more dead time
// (more rejects at rate); short window -> higher throughput but risks ballistic deficit.
module integrating_peak_detector #(
    parameter int WIDTH       = 12,
    parameter int THRESH      = 80,   // trigger: counts above baseline
    parameter int END_THRESH  = 40,   // "returned to baseline" hysteresis
    parameter int DERIV_D     = 4,    // derivative span (samples) for arrival detection
    parameter int RISE_THRESH = 120,  // slope over DERIV_D that marks a new pulse arrival
    parameter int HOLDOFF     = 4     // ignore arrivals this many samples after the trigger
)(
    input  logic              clk,
    input  logic              rst_n,
    input  logic              sample_valid,
    input  logic [WIDTH-1:0]  adc,
    input  logic [15:0]       window,     // integration length (trigger sample + `window` more
                                          // = window+1 samples total; the study labels window*10 ns)
    output logic              evt_valid,   // 1-cycle strobe per finished event
    output logic [31:0]       evt_energy,  // integrated charge (sum of adc-baseline)
    output logic [31:0]       evt_time,    // sample index of the trigger
    output logic              evt_reject   // 1 = piled up, energy not trustworthy
);
    typedef enum logic {S_IDLE, S_BUSY} state_t;
    state_t state;

    int          baseline;
    logic        seeded;
    logic [31:0] sample_idx;
    logic signed [31:0] acc;
    logic [15:0] timer;
    logic        piled;
    logic [31:0] trig_time;

    // short history for the derivative (fast) channel
    logic [WIDTH-1:0] hist [0:7];
    logic        prev_arr;

    int   sig, slope;
    logic above, arr, new_arr;

    always_ff @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            state <= S_IDLE; seeded <= 1'b0; baseline <= 0; sample_idx <= 0;
            acc <= 0; timer <= 0; piled <= 1'b0; prev_arr <= 1'b0;
            evt_valid <= 1'b0; evt_energy <= 0; evt_time <= 0; evt_reject <= 1'b0;
            for (int i = 0; i < 8; i++) hist[i] <= '0;
        end else begin
            evt_valid <= 1'b0;
            if (sample_valid) begin
                sample_idx <= sample_idx + 1;

                // shift history (adc[n-1] .. adc[n-8])
                for (int i = 7; i > 0; i--) hist[i] <= hist[i-1];
                hist[0] <= adc;

                if (!seeded) begin
                    baseline <= int'(adc);
                    seeded   <= 1'b1;
                end else begin
                    sig    = int'(adc) - baseline;
                    above  = (sig > THRESH);
                    slope  = int'(adc) - int'(hist[DERIV_D-1]);   // rise over DERIV_D samples
                    arr    = (slope > RISE_THRESH);
                    new_arr = arr & ~prev_arr;                    // one strobe per rising pulse
                    prev_arr <= arr;

                    case (state)
                        S_IDLE: begin
                            if (int'(adc) > baseline)      baseline <= baseline + 1;
                            else if (int'(adc) < baseline) baseline <= baseline - 1;
                            if (above) begin
                                state     <= S_BUSY;
                                acc       <= sig;
                                timer     <= (window == 0) ? 16'd1 : window - 1;
                                piled     <= 1'b0;
                                trig_time <= sample_idx;
                            end
                        end
                        S_BUSY: begin
                            acc <= acc + sig;
                            // a 2nd arrival after the trigger's own rise (holdoff) = pile-up
                            if (new_arr && (sample_idx - trig_time) >= HOLDOFF) piled <= 1'b1;

                            if (timer == 0) begin
                                evt_valid  <= 1'b1;
                                evt_energy <= acc + sig;
                                evt_time   <= trig_time;
                                // reject: a 2nd pulse landed in the window (incl. this last
                                // sample — `piled` is registered, so test new_arr directly too),
                                // or the signal is still up at window end (tail pile-up).
                                evt_reject <= piled
                                           || (new_arr && (sample_idx - trig_time) >= HOLDOFF)
                                           || (sig >= END_THRESH);
                                state      <= S_IDLE;
                            end else begin
                                timer <= timer - 16'd1;
                            end
                        end
                        default: state <= S_IDLE;
                    endcase
                end
            end
        end
    end
endmodule
