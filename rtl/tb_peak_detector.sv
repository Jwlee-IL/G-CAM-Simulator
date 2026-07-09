// Self-checking testbench: streams ADC samples from adc.txt into the peak
// detector (one sample per clock) and writes every detected peak to peaks.txt
// as "<peak_time> <peak_amp>". Analysis/scoring is done in Python afterwards.
`timescale 1ns/1ps
module tb;
    localparam int WIDTH = 12;

    logic              clk = 1'b0;
    logic              rst_n = 1'b0;
    logic              sample_valid = 1'b0;
    logic [WIDTH-1:0]  adc = '0;
    logic              peak_valid;
    logic [WIDTH-1:0]  peak_amp;
    logic [31:0]       peak_time;

    peak_detector #(.WIDTH(WIDTH)) dut (.*);

    always #5 clk = ~clk;   // 100 MHz (10 ns period)

    integer fd_in, fd_out, code, val, npk;

    // Record every detected peak.
    always @(posedge clk) begin
        if (peak_valid) begin
            $fwrite(fd_out, "%0d %0d\n", peak_time, peak_amp);
            npk = npk + 1;
        end
    end

    initial begin
        npk    = 0;
        fd_in  = $fopen("adc.txt", "r");
        fd_out = $fopen("peaks.txt", "w");
        if (fd_in == 0 || fd_out == 0) begin
            $display("ERROR: cannot open adc.txt / peaks.txt");
            $finish;
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

        repeat (20) @(posedge clk);
        $display("TB done: %0d peaks detected", npk);
        $fclose(fd_in);
        $fclose(fd_out);
        $finish;
    end
endmodule
