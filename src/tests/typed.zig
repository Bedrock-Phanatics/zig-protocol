const std = @import("std");
const root = @import("../root.zig");

test "play_status uses a canonical independent fixture" {
    const wire = &.{ 2, 0, 0, 0, 3 };
    const decoded = try root.typed.decode(.Current, wire, .{});

    var out: [64]u8 = undefined;
    var w = root.Writer.init(&out);
    try root.typed.encode(.Current, &w, decoded);
    try std.testing.expectEqualSlices(u8, wire, w.written());
}

test "login roundtrips through decode/encode" {
    var buf: [128]u8 = undefined;
    var w = root.Writer.init(&buf);

    const original = root.packets.login.Current{
        .protocol_version = 2169,
        .connection_request = "fake-jwt-chain",
    };
    try root.codecs.login.encode(.Current, &w, original);

    var r = try root.Reader.init(w.written(), .{});
    const decoded = try root.codecs.login.decode(.Current, &r);

    try std.testing.expectEqual(original.protocol_version, decoded.protocol_version);
    try std.testing.expectEqualSlices(u8, original.connection_request, decoded.connection_request);
}

test "typed decoder rejects trailing bytes and packet ID mismatch" {
    try std.testing.expectError(error.TrailingData, root.typed.decode(.Current, &.{ 2, 0, 0, 0, 3, 0xff }, .{}));

    var out: [16]u8 = undefined;
    var w = root.Writer.init(&out);
    try std.testing.expectError(error.InvalidPacketId, root.typed.encode(.Current, &w, .{
        .header = .{ .packet_id = 1 },
        .packet = .{ .play_status = .{ .status = .login_success } },
    }));
    try std.testing.expectEqual(@as(usize, 0), w.written().len);
}

test "typed decoder treats unimplemented but valid protocol IDs as unknown" {
    const decoded = try root.typed.decode(.Current, &.{99}, .{});
    try std.testing.expect(decoded.packet == .unknown);
    try std.testing.expect(decoded.packet.unknown.known);
    try std.testing.expectEqual(@as(u10, 99), decoded.packet.unknown.packet_id);
    try std.testing.expectEqual(@as(usize, 0), decoded.packet.unknown.raw.len);
}

test "typed decoder marks nonexistent packet IDs as unknown and not known" {
    const decoded = try root.typed.decode(.Current, &.{20}, .{});
    try std.testing.expect(decoded.packet == .unknown);
    try std.testing.expect(!decoded.packet.unknown.known);
    try std.testing.expectEqual(@as(u10, 20), decoded.packet.unknown.packet_id);
}

test "typed decoder preserves unknown packet payload bytes" {
    const wire = &.{ 99, 0xaa, 0xbb, 0xcc };
    const decoded = try root.typed.decode(.Current, wire, .{});
    try std.testing.expect(decoded.packet == .unknown);
    try std.testing.expectEqualSlices(u8, &.{ 0xaa, 0xbb, 0xcc }, decoded.packet.unknown.raw);
}

test "typed encoder roundtrips unknown packets" {
    const wire = &.{ 99, 0xaa, 0xbb, 0xcc };
    const decoded = try root.typed.decode(.Current, wire, .{});

    var out: [16]u8 = undefined;
    var w = root.Writer.init(&out);
    try root.typed.encode(.Current, &w, decoded);
    try std.testing.expectEqualSlices(u8, wire, w.written());
}

test "typed encoder rejects header/packet id mismatch for unknown packets" {
    var out: [16]u8 = undefined;
    var w = root.Writer.init(&out);
    try std.testing.expectError(error.InvalidPacketId, root.typed.encode(.Current, &w, .{
        .header = .{ .packet_id = 100 },
        .packet = .{ .unknown = .{ .packet_id = 99, .raw = &.{}, .known = true } },
    }));
}

test "play_status decode rejects invalid enum values" {
    var r = try root.Reader.init(&.{ 0, 0, 0, 99 }, .{});
    try std.testing.expectError(error.InvalidEnum, root.codecs.play_status.decode(.Current, &r));
}
