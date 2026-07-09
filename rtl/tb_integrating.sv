// Testbench for integrating_peak_detector: streams adc.txt, integration `window` from a
// +WINDOW=<n> plusarg, writes each event "<time> <energy> <reject>" to peaks_int.txt.
`timescale 1ns/1ps
module tb;
    localparam int WIDTH = 12;

    logic              clk = 1'b0;
    logic              rst_n = 1'b0;
    logic              sample_valid = 1'b0;
    logic [WIDTH-1:0]  adc = '0;
    logic [15:0]       window;
    logic              evt_valid;
    logic [31:0]       evt_energy;
    logic [31:0]       evt_time;
    logic              evt_reject;

    integrating_peak_detector #(.WIDTH(WIDTH)) dut (.*);

    always #5 clk = ~clk;   // 100 MHz

    integer fd_in, fd_out, code, val, npk;
    integer win_arg;

    always @(posedge clk) begin
        if (evt_valid) begin
            $fwrite(fd_out, "%0d %0d %0d\n", evt_time, evt_energy, evt_reject);
            npk = npk + 1;
        end
    end

    initial begin
        npk = 0;
        win_arg = 50;
        if (!$value$plusargs("WINDOW=%d", win_arg)) win_arg = 50;
        window = win_arg[15:0];

        fd_in  = $fopen("adc.txt", "r");
        fd_out = $fopen("peaks_int.txt", "w");
        if (fd_in == 0 || fd_out == 0) begin
            $display("ERROR: cannot open adc.txt / peaks_int.txt"); $finish;
        end

        repeat (4) @(posedge clk);
        rst_n = 1'b1;
        @(posedge clk);

        code = $fscanf(fd_in, "%d\n", val);
        while (code == 1) begin
            sample_valid <= 1'b1;
            adc          <= val[WIDTH-1:0];
            @(posedge clk);
            code = $fscanf(fd_in, "%d\n", val);
        end
        sample_valid <= 1'b0;

        repeat (window + 20) @(posedge clk);
        $display("TB done: %0d events (window=%0d)", npk, window);
        $fclose(fd_in); $fclose(fd_out);
        $finish;
    end
endmodule
