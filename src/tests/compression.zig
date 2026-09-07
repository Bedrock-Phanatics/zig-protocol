const std = @import("std");
const root = @import("../root.zig");
test "known raw DEFLATE fixture and output bomb bound" {
    const compressed = [_]u8{ 0x73, 0x74, 0x1c, 0x05, 0xa3, 0x60, 0x14, 0x0c, 0x77, 0x00, 0x00 };
    var output: [1024]u8 = undefined;
    var history: [std.compress.flate.max_window_len]u8 = undefined;
    const plain = try root.batch.decompressDeflate(&compressed, &output, &history, .{});
    try std.testing.expectEqual(@as(usize, 1000), plain.len);
    for (plain) |v| try std.testing.expectEqual(@as(u8, 'A'), v);
    var tiny: [16]u8 = undefined;
    try std.testing.expectError(error.LimitExceeded, root.batch.decompressDeflate(&compressed, &tiny, &history, .{}));
}
test "batch framing rejects empty packets and excessive counts" {
    var storage: [16]u8 = undefined;
    var w = root.Writer.init(&storage);
    try std.testing.expectError(error.InvalidValue, root.batch.writePacket(&w, &.{}, .{}));
    try root.batch.writePacket(&w, &.{1}, .{});
    try root.batch.writePacket(&w, &.{2}, .{});
    var it = try root.batch.Iterator.init(w.written(), .{ .max_packets_per_batch = 1 });
    _ = (try it.next()).?;
    try std.testing.expectError(error.LimitExceeded, it.next());
}
