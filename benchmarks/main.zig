const std = @import("std");
const protocol = @import("bedrock_protocol");
pub fn main(init: std.process.Init) !void {
    const io = init.io;
    var output: [256]u8 = undefined;
    var file = std.Io.File.stdout().writer(io, &output);
    const stdout = &file.interface;
    var storage: [10]u8 = undefined;
    var checksum: u64 = 0;
    const iterations: u64 = 5_000_000;
    const start = std.Io.Clock.awake.now(io).nanoseconds;
    for (0..iterations) |i| {
        var w = protocol.Writer.init(&storage);
        try w.writeVarU64(i);
        var r = try protocol.Reader.init(w.written(), .{});
        checksum +%= try r.readVarU64();
    }
    const elapsed = std.Io.Clock.awake.now(io).nanoseconds - start;
    const ns = @as(f64, @floatFromInt(elapsed)) / @as(f64, @floatFromInt(iterations));
    try stdout.print("varint round-trip: {d:.2} ns/op, {d:.2} Mops/s (checksum={d})\n", .{ ns, 1000.0 / ns, checksum });
    try stdout.flush();
}
