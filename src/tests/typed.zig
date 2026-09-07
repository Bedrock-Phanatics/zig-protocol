const std = @import("std");
const root = @import("../root.zig");
test "typed control packets use canonical independent fixtures" {
    const cases = [_][]const u8{ &.{ 2, 0, 0, 0, 3 }, &.{ 10, 0xdf, 0x5d }, &.{ 42, 40 }, &.{ 59, 1 }, &.{ 60, 3 }, &.{ 70, 20 }, &.{ 0xc1, 0x01, 0, 0, 8, 0x90 } };
    for (cases) |wire| {
        const decoded = try root.typed.decode(wire, .{});
        var out: [64]u8 = undefined;
        var w = root.Writer.init(&out);
        try root.typed.encode(&w, decoded);
        try std.testing.expectEqualSlices(u8, wire, w.written());
    }
}
test "typed decoder rejects trailing bytes unknown IDs and packet ID mismatch" {
    try std.testing.expectError(error.TrailingData, root.typed.decode(&.{ 59, 1, 0 }, .{}));
    try std.testing.expectError(error.InvalidPacketId, root.typed.decode(&.{99}, .{}));
    var out: [16]u8 = undefined;
    var w = root.Writer.init(&out);
    try std.testing.expectError(error.InvalidValue, root.typed.encode(&w, .{ .header = .{ .packet_id = 42 }, .packet = .{ .set_difficulty = .{ .difficulty = 1 } } }));
}
test "teleport presence must match move mode" {
    var out: [128]u8 = undefined;
    var w = root.Writer.init(&out);
    const p = root.packets.move_player.MovePlayerPacket{ .entity_runtime_id = 1, .position = .{ .x = 0, .y = 0, .z = 0 }, .rotation = .{ .x = 0, .y = 0, .z = 0 }, .mode = .normal, .on_ground = true, .ridden_entity_runtime_id = 0, .teleport = .{ .cause = 1, .source_entity_type = 2 }, .tick = 1 };
    try std.testing.expectError(error.InvalidValue, root.codecs.move_player.encode(&w, p));
}
