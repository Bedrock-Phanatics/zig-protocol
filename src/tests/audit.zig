const std = @import("std");
const root = @import("../root.zig");

test "fixed integers UUID vectors and batch framing" {
    var storage: [256]u8 = undefined;
    var w = root.Writer.init(&storage);
    const uuid = [_]u8{ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
    try w.writeU16(0x1234);
    try w.writeU32Be(0x89abcdef);
    try w.writeUuid(uuid);
    var r = try root.Reader.init(w.written(), .{});
    try std.testing.expectEqual(@as(u16, 0x1234), try r.readU16());
    try std.testing.expectEqual(@as(u32, 0x89abcdef), try r.readU32Be());
    try std.testing.expectEqual(uuid, try r.readUuid());
    try r.finish();
    var framed: [32]u8 = undefined;
    var fw = root.Writer.init(&framed);
    try root.batch.writePacket(&fw, &.{ 1, 2, 3 }, .{});
    try root.batch.writePacket(&fw, &.{4}, .{});
    var it = try root.batch.Iterator.init(fw.written(), .{});
    try std.testing.expectEqualSlices(u8, &.{ 1, 2, 3 }, (try it.next()).?);
    try std.testing.expectEqualSlices(u8, &.{4}, (try it.next()).?);
    try std.testing.expectEqual(@as(?[]const u8, null), try it.next());
}

test "truncation never advances beyond input" {
    var r = try root.Reader.init(&.{ 0xff, 0xff, 0xff }, .{});
    try std.testing.expectError(error.EndOfStream, r.readU64());
    try std.testing.expect(r.cursor <= r.input.len);
}

test "bounded parser stress on deterministic hostile input" {
    var prng = std.Random.DefaultPrng.init(0xbed0c2192);
    const random = prng.random();
    var bytes: [96]u8 = undefined;
    for (0..20_000) |_| {
        random.bytes(&bytes);
        const len = random.intRangeAtMost(usize, 0, bytes.len);
        var r = root.Reader.init(bytes[0..len], .{ .max_packet_bytes = bytes.len }) catch continue;
        _ = r.readVarU64() catch {};
        std.testing.expect(r.cursor <= r.input.len) catch unreachable;
    }
}

test "native fuzz target for reader and envelope" {
    try std.testing.fuzz({}, fuzzParser, .{});
}
fn fuzzParser(_: void, smith: *std.testing.Smith) !void {
    var bytes: [256]u8 = undefined;
    smith.bytesWithHash(&bytes, 0x5ef77a31);
    const len: usize = smith.valueRangeAtMostWithHash(u16, 0, bytes.len, 0x96653389);
    var r = root.Reader.init(bytes[0..len], .{ .max_packet_bytes = bytes.len, .max_string_bytes = 64, .max_byte_array_bytes = 128, .max_array_elements = 32 }) catch return;
    _ = r.readVarU32() catch {};
    try std.testing.expect(r.cursor <= r.input.len);
    _ = root.packet.decode(bytes[0..len], r.limits) catch {};
}
test "batch total limit is independent from per-packet limit" {
    const limits: root.DecodeLimits = .{ .max_packet_bytes = 4, .max_decompressed_batch_bytes = 16 };
    const framed = [_]u8{ 4, 1, 2, 3, 4, 4, 5, 6, 7, 8 };
    var it = try root.batch.Iterator.init(&framed, limits);
    try std.testing.expectEqualSlices(u8, &.{ 1, 2, 3, 4 }, (try it.next()).?);
    try std.testing.expectEqualSlices(u8, &.{ 5, 6, 7, 8 }, (try it.next()).?);
    try std.testing.expectEqual(@as(?[]const u8, null), try it.next());

    var oversized = try root.batch.Iterator.init(&.{ 5, 1, 2, 3, 4, 5 }, limits);
    try std.testing.expectError(error.LimitExceeded, oversized.next());
}

test "invalid reader checkpoint is rejected without changing cursor" {
    var r = try root.Reader.init(&.{0x2a}, .{});
    try std.testing.expectError(error.InvalidCheckpoint, r.restore(2));
    try std.testing.expectEqual(@as(usize, 0), r.cursor);
    try std.testing.expectEqual(@as(u8, 0x2a), try r.readU8());
}
