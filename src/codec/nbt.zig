const std = @import("std");
const Reader = @import("reader.zig").Reader;
pub const Tag = enum(u8) { end = 0, byte = 1, short = 2, int = 3, long = 4, float = 5, double = 6, byte_array = 7, string = 8, list = 9, compound = 10, int_array = 11, long_array = 12 };
/// Validates and skips one complete Bedrock network-little-endian NBT document without allocating.
/// The returned slice borrows the reader input and contains the exact encoded document.
pub fn readDocument(r: *Reader) ![]const u8 {
    const start = r.cursor;
    const tag = std.enums.fromInt(Tag, try r.readU8()) orelse return error.InvalidNbt;
    if (tag == .end) return error.InvalidNbt;
    _ = try readNbtString(r);
    try skipPayload(r, tag, 0, start);
    return r.input[start..r.cursor];
}
fn readNbtString(r: *Reader) ![]const u8 {
    const len: usize = try r.readVarU32();
    if (len > 32767 or len > r.limits.max_string_bytes) return error.LimitExceeded;
    const value = try r.take(len);
    if (!std.unicode.utf8ValidateSlice(value)) return error.InvalidUtf8;
    return value;
}
fn count(r: *Reader) !usize {
    const value = try r.readVarI32();
    if (value < 0) return error.InvalidNbt;
    const result: usize = @intCast(value);
    if (result > r.limits.max_array_elements) return error.LimitExceeded;
    return result;
}
fn checkBudget(r: *Reader, start: usize) !void {
    if (r.cursor - start > r.limits.max_nbt_bytes) return error.LimitExceeded;
}
fn skipPayload(r: *Reader, tag: Tag, depth: usize, start: usize) !void {
    if (depth > r.limits.max_nesting_depth) return error.LimitExceeded;
    switch (tag) {
        .end => return error.InvalidNbt,
        .byte => _ = try r.readI8(),
        .short => _ = try r.readI16(),
        .int => _ = try r.readVarI32(),
        .long => _ = try r.readVarI64(),
        .float => _ = try r.readF32(),
        .double => _ = try r.readF64(),
        .byte_array => {
            const n = try count(r);
            if (n > r.limits.max_byte_array_bytes) return error.LimitExceeded;
            _ = try r.take(n);
        },
        .string => _ = try readNbtString(r),
        .list => {
            const child = std.enums.fromInt(Tag, try r.readU8()) orelse return error.InvalidNbt;
            const n = try count(r);
            if (child == .end and n != 0) return error.InvalidNbt;
            for (0..n) |_| {
                try skipPayload(r, child, depth + 1, start);
            }
        },
        .compound => {
            while (true) {
                const child = std.enums.fromInt(Tag, try r.readU8()) orelse return error.InvalidNbt;
                if (child == .end) break;
                _ = try readNbtString(r);
                try skipPayload(r, child, depth + 1, start);
            }
        },
        .int_array => {
            const n = try count(r);
            for (0..n) |_| _ = try r.readVarI32();
        },
        .long_array => {
            const n = try count(r);
            for (0..n) |_| _ = try r.readVarI64();
        },
    }
    try checkBudget(r, start);
}
