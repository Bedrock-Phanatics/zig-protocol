const std = @import("std");
const root = @import("../root.zig");

test "canonical unsigned varints round trip" {
    const values = [_]u32{ 0, 1, 127, 128, 16384, std.math.maxInt(u32) };
    for (values) |expected| {
        var storage: [10]u8 = undefined;
        var writer = root.Writer.init(&storage);
        try writer.writeVarU32(expected);
        var reader = try root.Reader.init(writer.written(), .{});
        try std.testing.expectEqual(expected, try reader.readVarU32());
        try reader.finish();
    }
}

test "signed zigzag boundaries round trip" {
    const values = [_]i32{ std.math.minInt(i32), -1, 0, 1, 127, 128, std.math.maxInt(i32) };
    for (values) |expected| {
        var storage: [10]u8 = undefined;
        var writer = root.Writer.init(&storage);
        try writer.writeVarI32(expected);
        var reader = try root.Reader.init(writer.written(), .{});
        try std.testing.expectEqual(expected, try reader.readVarI32());
    }
}

test "malformed and non-canonical varints are rejected" {
    const cases = [_][]const u8{ &.{0x80}, &.{ 0x80, 0x00 }, &.{ 0xff, 0xff, 0xff, 0xff, 0x10 } };
    for (cases) |bytes| {
        var reader = try root.Reader.init(bytes, .{});
        try std.testing.expectError(if (bytes.len == 2) error.NonCanonicalVarInt else if (bytes.len == 1) error.EndOfStream else error.VarIntOverflow, reader.readVarU32());
    }
}

test "limits and UTF-8 are enforced without allocation" {
    var oversized = try root.Reader.init(&.{ 4, 't', 'e', 's', 't' }, .{ .max_string_bytes = 3 });
    try std.testing.expectError(error.LimitExceeded, oversized.readString());
    var invalid = try root.Reader.init(&.{ 2, 0xc3, 0x28 }, .{});
    try std.testing.expectError(error.InvalidUtf8, invalid.readString());
}
