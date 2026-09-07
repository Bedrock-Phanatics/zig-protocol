const std = @import("std");
const DecodeLimits = @import("limits.zig").DecodeLimits;
const DecodeError = @import("errors.zig").DecodeError;
const vector = @import("../types/vector.zig");
const BlockPosition = @import("../types/block_position.zig").BlockPosition;
const position = @import("../types/position.zig");
const colour = @import("../types/colour.zig");

pub const Reader = struct {
    input: []const u8,
    cursor: usize = 0,
    limits: DecodeLimits,

    pub fn init(input: []const u8, limits: DecodeLimits) DecodeError!Reader {
        return initWithInputLimit(input, limits, limits.max_packet_bytes);
    }
    pub fn initWithInputLimit(input: []const u8, limits: DecodeLimits, max_input_bytes: usize) DecodeError!Reader {
        if (!limits.valid() or input.len > max_input_bytes) return error.LimitExceeded;
        return .{ .input = input, .limits = limits };
    }
    pub inline fn remaining(self: *const Reader) usize {
        return self.input.len - self.cursor;
    }
    pub inline fn end(self: *const Reader) bool {
        return self.cursor == self.input.len;
    }
    pub inline fn checkpoint(self: *const Reader) usize {
        return self.cursor;
    }
    pub fn restore(self: *Reader, mark: usize) DecodeError!void {
        if (mark > self.input.len) return error.InvalidCheckpoint;
        self.cursor = mark;
    }
    pub fn take(self: *Reader, count: usize) DecodeError![]const u8 {
        if (count > self.remaining()) return error.EndOfStream;
        const start = self.cursor;
        self.cursor += count;
        return self.input[start..self.cursor];
    }
    pub inline fn readU8(self: *Reader) DecodeError!u8 {
        return (try self.take(1))[0];
    }
    pub inline fn readI8(self: *Reader) DecodeError!i8 {
        return @bitCast(try self.readU8());
    }
    pub fn readBool(self: *Reader) DecodeError!bool {
        return switch (try self.readU8()) {
            0 => false,
            1 => true,
            else => error.InvalidBoolean,
        };
    }
    fn readInt(self: *Reader, comptime T: type, endian: std.builtin.Endian) DecodeError!T {
        const bytes = try self.take(@sizeOf(T));
        return std.mem.readInt(T, @ptrCast(bytes.ptr), endian);
    }
    pub inline fn readU16(self: *Reader) DecodeError!u16 {
        return self.readInt(u16, .little);
    }
    pub inline fn readI16(self: *Reader) DecodeError!i16 {
        return self.readInt(i16, .little);
    }
    pub inline fn readU32(self: *Reader) DecodeError!u32 {
        return self.readInt(u32, .little);
    }
    pub inline fn readI32(self: *Reader) DecodeError!i32 {
        return self.readInt(i32, .little);
    }
    pub inline fn readU64(self: *Reader) DecodeError!u64 {
        return self.readInt(u64, .little);
    }
    pub inline fn readI64(self: *Reader) DecodeError!i64 {
        return self.readInt(i64, .little);
    }
    pub inline fn readU16Be(self: *Reader) DecodeError!u16 {
        return self.readInt(u16, .big);
    }
    pub inline fn readI16Be(self: *Reader) DecodeError!i16 {
        return self.readInt(i16, .big);
    }
    pub inline fn readU32Be(self: *Reader) DecodeError!u32 {
        return self.readInt(u32, .big);
    }
    pub inline fn readI32Be(self: *Reader) DecodeError!i32 {
        return self.readInt(i32, .big);
    }
    pub inline fn readU64Be(self: *Reader) DecodeError!u64 {
        return self.readInt(u64, .big);
    }
    pub inline fn readI64Be(self: *Reader) DecodeError!i64 {
        return self.readInt(i64, .big);
    }
    pub inline fn readF32(self: *Reader) DecodeError!f32 {
        return @bitCast(try self.readU32());
    }
    pub inline fn readF64(self: *Reader) DecodeError!f64 {
        return @bitCast(try self.readU64());
    }

    pub fn readVarU32(self: *Reader) DecodeError!u32 {
        var value: u32 = 0;
        var index: u3 = 0;
        while (index < 5) : (index += 1) {
            const byte = try self.readU8();
            if (index == 4 and byte & 0xf0 != 0) return error.VarIntOverflow;
            value |= @as(u32, byte & 0x7f) << @as(u5, index) * 7;
            if (byte & 0x80 == 0) {
                if (index != 0 and byte == 0) return error.NonCanonicalVarInt;
                return value;
            }
        }
        return error.VarIntOverflow;
    }
    pub fn readVarU64(self: *Reader) DecodeError!u64 {
        var value: u64 = 0;
        var index: u4 = 0;
        while (index < 10) : (index += 1) {
            const byte = try self.readU8();
            if (index == 9 and byte & 0xfe != 0) return error.VarIntOverflow;
            value |= @as(u64, byte & 0x7f) << @as(u6, index) * 7;
            if (byte & 0x80 == 0) {
                if (index != 0 and byte == 0) return error.NonCanonicalVarInt;
                return value;
            }
        }
        return error.VarIntOverflow;
    }
    pub inline fn readVarI32(self: *Reader) DecodeError!i32 {
        const v = try self.readVarU32();
        return @bitCast((v >> 1) ^ (0 -% (v & 1)));
    }
    pub inline fn readVarI64(self: *Reader) DecodeError!i64 {
        const v = try self.readVarU64();
        return @bitCast((v >> 1) ^ (0 -% (v & 1)));
    }
    fn boundedLength(self: *Reader, maximum: usize) DecodeError!usize {
        const v: usize = try self.readVarU32();
        if (v > maximum) return error.LimitExceeded;
        return v;
    }
    pub fn readString(self: *Reader) DecodeError![]const u8 {
        const bytes = try self.take(try self.boundedLength(self.limits.max_string_bytes));
        if (!std.unicode.utf8ValidateSlice(bytes)) return error.InvalidUtf8;
        return bytes;
    }
    pub fn readByteArray(self: *Reader) DecodeError![]const u8 {
        return self.take(try self.boundedLength(self.limits.max_byte_array_bytes));
    }
    pub fn readCollectionLength(self: *Reader) DecodeError!usize {
        return self.boundedLength(self.limits.max_array_elements);
    }
    pub fn readVec2f(self: *Reader) DecodeError!vector.Vec2f {
        return .{ .x = try self.readF32(), .y = try self.readF32() };
    }
    pub fn readVec3f(self: *Reader) DecodeError!vector.Vec3f {
        return .{ .x = try self.readF32(), .y = try self.readF32(), .z = try self.readF32() };
    }
    pub fn readBlockPosition(self: *Reader) DecodeError!BlockPosition {
        return .{ .x = try self.readVarI32(), .y = try self.readVarI32(), .z = try self.readVarI32() };
    }
    pub fn readUuid(self: *Reader) DecodeError![16]u8 {
        const src = try self.take(16);
        var out: [16]u8 = undefined;
        for (0..8) |i| {
            out[i] = src[7 - i];
            out[i + 8] = src[15 - i];
        }
        return out;
    }
    pub fn readRemaining(self: *Reader) []const u8 {
        const value = self.input[self.cursor..];
        self.cursor = self.input.len;
        return value;
    }
    pub fn readChunkPosition(self: *Reader) DecodeError!position.ChunkPosition {
        return .{ .x = try self.readVarI32(), .z = try self.readVarI32() };
    }
    pub fn readSubChunkPosition(self: *Reader) DecodeError!position.SubChunkPosition {
        return .{ .x = try self.readI32(), .y = try self.readI32(), .z = try self.readI32() };
    }
    pub fn readSoundPosition(self: *Reader) DecodeError!vector.Vec3f {
        const p = try self.readBlockPosition();
        return .{ .x = @as(f32, @floatFromInt(p.x)) / 8.0, .y = @as(f32, @floatFromInt(p.y)) / 8.0, .z = @as(f32, @floatFromInt(p.z)) / 8.0 };
    }
    pub fn readByteFloat(self: *Reader) DecodeError!f32 {
        return @as(f32, @floatFromInt(try self.readU8())) * (360.0 / 256.0);
    }
    pub fn readRgba(self: *Reader) DecodeError!colour.Rgba {
        return @bitCast(try self.readU32());
    }
    pub fn readBeArgb(self: *Reader) DecodeError!colour.Rgba {
        const value = try self.readU32Be();
        return .{ .r = @truncate(value >> 8), .g = @truncate(value >> 16), .b = @truncate(value >> 24), .a = @truncate(value) };
    }
    pub fn finish(self: *const Reader) DecodeError!void {
        if (!self.end()) return error.TrailingData;
    }
};
