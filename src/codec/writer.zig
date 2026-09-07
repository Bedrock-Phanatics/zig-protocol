const std = @import("std");
const vector = @import("../types/vector.zig");
const BlockPosition = @import("../types/block_position.zig").BlockPosition;
const position = @import("../types/position.zig");
const colour = @import("../types/colour.zig");

pub const Writer = struct {
    buffer: []u8,
    cursor: usize = 0,
    pub fn init(buffer: []u8) Writer {
        return .{ .buffer = buffer };
    }
    pub inline fn written(self: *const Writer) []const u8 {
        return self.buffer[0..self.cursor];
    }
    pub inline fn remainingCapacity(self: *const Writer) usize {
        return self.buffer.len - self.cursor;
    }
    pub fn writeRaw(self: *Writer, bytes: []const u8) error{NoSpaceLeft}!void {
        if (bytes.len > self.remainingCapacity()) return error.NoSpaceLeft;
        @memcpy(self.buffer[self.cursor..][0..bytes.len], bytes);
        self.cursor += bytes.len;
    }
    pub inline fn writeU8(self: *Writer, v: u8) error{NoSpaceLeft}!void {
        if (self.remainingCapacity() == 0) return error.NoSpaceLeft;
        self.buffer[self.cursor] = v;
        self.cursor += 1;
    }
    pub inline fn writeI8(self: *Writer, v: i8) error{NoSpaceLeft}!void {
        try self.writeU8(@bitCast(v));
    }
    pub inline fn writeBool(self: *Writer, v: bool) error{NoSpaceLeft}!void {
        try self.writeU8(@intFromBool(v));
    }
    fn writeInt(self: *Writer, comptime T: type, v: T, endian: std.builtin.Endian) error{NoSpaceLeft}!void {
        if (@sizeOf(T) > self.remainingCapacity()) return error.NoSpaceLeft;
        std.mem.writeInt(T, self.buffer[self.cursor..][0..@sizeOf(T)], v, endian);
        self.cursor += @sizeOf(T);
    }
    pub inline fn writeU16(self: *Writer, v: u16) !void {
        try self.writeInt(u16, v, .little);
    }
    pub inline fn writeI16(self: *Writer, v: i16) !void {
        try self.writeInt(i16, v, .little);
    }
    pub inline fn writeU32(self: *Writer, v: u32) !void {
        try self.writeInt(u32, v, .little);
    }
    pub inline fn writeI32(self: *Writer, v: i32) !void {
        try self.writeInt(i32, v, .little);
    }
    pub inline fn writeU64(self: *Writer, v: u64) !void {
        try self.writeInt(u64, v, .little);
    }
    pub inline fn writeI64(self: *Writer, v: i64) !void {
        try self.writeInt(i64, v, .little);
    }
    pub inline fn writeU16Be(self: *Writer, v: u16) !void {
        try self.writeInt(u16, v, .big);
    }
    pub inline fn writeI16Be(self: *Writer, v: i16) !void {
        try self.writeInt(i16, v, .big);
    }
    pub inline fn writeU32Be(self: *Writer, v: u32) !void {
        try self.writeInt(u32, v, .big);
    }
    pub inline fn writeI32Be(self: *Writer, v: i32) !void {
        try self.writeInt(i32, v, .big);
    }
    pub inline fn writeU64Be(self: *Writer, v: u64) !void {
        try self.writeInt(u64, v, .big);
    }
    pub inline fn writeI64Be(self: *Writer, v: i64) !void {
        try self.writeInt(i64, v, .big);
    }
    pub inline fn writeF32(self: *Writer, v: f32) !void {
        try self.writeU32(@bitCast(v));
    }
    pub inline fn writeF64(self: *Writer, v: f64) !void {
        try self.writeU64(@bitCast(v));
    }
    pub fn writeVarU32(self: *Writer, initial: u32) !void {
        var v = initial;
        while (v >= 0x80) {
            try self.writeU8(@truncate(v | 0x80));
            v >>= 7;
        }
        try self.writeU8(@truncate(v));
    }
    pub fn writeVarU64(self: *Writer, initial: u64) !void {
        var v = initial;
        while (v >= 0x80) {
            try self.writeU8(@truncate(v | 0x80));
            v >>= 7;
        }
        try self.writeU8(@truncate(v));
    }
    pub inline fn writeVarI32(self: *Writer, v: i32) !void {
        const bits: u32 = @bitCast(v);
        try self.writeVarU32((bits << 1) ^ @as(u32, @bitCast(v >> 31)));
    }
    pub inline fn writeVarI64(self: *Writer, v: i64) !void {
        const bits: u64 = @bitCast(v);
        try self.writeVarU64((bits << 1) ^ @as(u64, @bitCast(v >> 63)));
    }
    pub fn writeString(self: *Writer, v: []const u8) error{ NoSpaceLeft, InvalidValue }!void {
        if (!std.unicode.utf8ValidateSlice(v) or v.len > std.math.maxInt(u32)) return error.InvalidValue;
        try self.writeVarU32(@intCast(v.len));
        try self.writeRaw(v);
    }
    pub fn writeByteArray(self: *Writer, v: []const u8) error{ NoSpaceLeft, InvalidValue }!void {
        if (v.len > std.math.maxInt(u32)) return error.InvalidValue;
        try self.writeVarU32(@intCast(v.len));
        try self.writeRaw(v);
    }
    pub fn writeVec2f(self: *Writer, v: vector.Vec2f) !void {
        try self.writeF32(v.x);
        try self.writeF32(v.y);
    }
    pub fn writeVec3f(self: *Writer, v: vector.Vec3f) !void {
        try self.writeF32(v.x);
        try self.writeF32(v.y);
        try self.writeF32(v.z);
    }
    pub fn writeBlockPosition(self: *Writer, v: BlockPosition) !void {
        try self.writeVarI32(v.x);
        try self.writeVarI32(v.y);
        try self.writeVarI32(v.z);
    }
    pub fn writeUuid(self: *Writer, v: [16]u8) !void {
        var wire: [16]u8 = undefined;
        for (0..8) |i| {
            wire[i] = v[7 - i];
            wire[i + 8] = v[15 - i];
        }
        try self.writeRaw(&wire);
    }
    pub fn writeChunkPosition(self: *Writer, v: position.ChunkPosition) !void {
        try self.writeVarI32(v.x);
        try self.writeVarI32(v.z);
    }
    pub fn writeSubChunkPosition(self: *Writer, v: position.SubChunkPosition) !void {
        try self.writeI32(v.x);
        try self.writeI32(v.y);
        try self.writeI32(v.z);
    }
    fn soundCoordinate(v: f32) error{InvalidValue}!i32 {
        if (!std.math.isFinite(v)) return error.InvalidValue;
        const scaled = @as(f64, v) * 8.0;
        if (scaled < @as(f64, @floatFromInt(std.math.minInt(i32))) or scaled > @as(f64, @floatFromInt(std.math.maxInt(i32)))) return error.InvalidValue;
        return @intFromFloat(scaled);
    }
    pub fn writeSoundPosition(self: *Writer, v: vector.Vec3f) !void {
        try self.writeBlockPosition(.{ .x = try soundCoordinate(v.x), .y = try soundCoordinate(v.y), .z = try soundCoordinate(v.z) });
    }
    pub fn writeByteFloat(self: *Writer, v: f32) !void {
        if (!std.math.isFinite(v)) return error.InvalidValue;
        const normalized = @mod(v, 360.0);
        try self.writeU8(@intFromFloat(normalized / (360.0 / 256.0)));
    }
    pub fn writeRgba(self: *Writer, v: colour.Rgba) !void {
        try self.writeU32(@bitCast(v));
    }
    pub fn writeBeArgb(self: *Writer, v: colour.Rgba) !void {
        const value = @as(u32, v.a) | @as(u32, v.r) << 8 | @as(u32, v.g) << 16 | @as(u32, v.b) << 24;
        try self.writeU32Be(value);
    }
};
