const std = @import("std");
const root = @import("../root.zig");
test "network NBT known fixture and malicious structures" {
    const fixture = [_]u8{ 10, 0, 3, 1, 'x', 2, 0 };
    var r = try root.Reader.init(&fixture, .{});
    try std.testing.expectEqualSlices(u8, &fixture, try root.nbt.readDocument(&r));
    try r.finish();
    var bad = try root.Reader.init(&.{ 9, 0, 0, 1 }, .{});
    try std.testing.expectError(error.InvalidNbt, root.nbt.readDocument(&bad));
}
test "NBT nesting limit is explicit" {
    const nested = [_]u8{ 10, 0, 10, 0, 10, 0, 10, 0, 0, 0, 0, 0 };
    var r = try root.Reader.init(&nested, .{ .max_nesting_depth = 1 });
    try std.testing.expectError(error.LimitExceeded, root.nbt.readDocument(&r));
}
