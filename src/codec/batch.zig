const std = @import("std");
const Reader = @import("reader.zig").Reader;
const Writer = @import("writer.zig").Writer;
const Limits = @import("limits.zig").DecodeLimits;
pub const header: u8 = 0xfe;
pub const Compression = enum(u8) { deflate = 0, snappy = 1, none = 0xff };
pub const Iterator = struct {
    reader: Reader,
    count: usize = 0,
    pub fn init(payload: []const u8, limits: Limits) !Iterator {
        if (payload.len > limits.max_decompressed_batch_bytes) return error.LimitExceeded;
        return .{ .reader = try Reader.initWithInputLimit(payload, limits, limits.max_decompressed_batch_bytes) };
    }
    pub fn next(s: *Iterator) !?[]const u8 {
        if (s.reader.end()) return null;
        if (s.count >= s.reader.limits.max_packets_per_batch) return error.LimitExceeded;
        const len: usize = try s.reader.readVarU32();
        if (len == 0) return error.InvalidPacketId;
        if (len > s.reader.limits.max_packet_bytes) return error.LimitExceeded;
        s.count += 1;
        return try s.reader.take(len);
    }
};
pub fn writePacket(w: *Writer, payload: []const u8, limits: Limits) !void {
    if (payload.len == 0) return error.InvalidValue;
    if (payload.len > limits.max_packet_bytes or payload.len > std.math.maxInt(u32)) return error.LimitExceeded;
    try w.writeVarU32(@intCast(payload.len));
    try w.writeRaw(payload);
}
pub fn unframed(frame: []const u8, limits: Limits) ![]const u8 {
    if (frame.len == 0 or frame[0] != header) return error.InvalidPacketId;
    if (frame.len > limits.max_batch_bytes) return error.LimitExceeded;
    return frame[1..];
}
/// Decompresses raw DEFLATE into caller-owned storage. No allocation or hidden retained state.
pub fn decompressDeflate(compressed: []const u8, output: []u8, history: *[std.compress.flate.max_window_len]u8, limits: Limits) ![]const u8 {
    if (output.len > limits.max_decompressed_batch_bytes) return error.LimitExceeded;
    var input: std.Io.Reader = .fixed(compressed);
    var decoder: std.compress.flate.Decompress = .init(&input, .raw, history);
    var out: std.Io.Writer = .fixed(output);
    const n = decoder.reader.streamRemaining(&out) catch {
        if (decoder.err != null) return error.InvalidCompressedData;
        return error.LimitExceeded;
    };
    if (n > limits.max_decompressed_batch_bytes) return error.LimitExceeded;
    if (input.seek != input.end) return error.TrailingData;
    return output[0..n];
}
