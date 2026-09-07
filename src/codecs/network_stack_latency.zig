const Reader = @import("../codec/reader.zig").Reader;
const Writer = @import("../codec/writer.zig").Writer;
const Packet = @import("../packets/network_stack_latency.zig").Packet;
pub fn decode(r: *Reader) !Packet {
    return .{ .timestamp = try r.readI64(), .needs_response = try r.readBool() };
}
pub fn encode(w: *Writer, p: Packet) !void {
    try w.writeI64(p.timestamp);
    try w.writeBool(p.needs_response);
}
