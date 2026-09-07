const Reader = @import("../codec/reader.zig").Reader;
const Writer = @import("../codec/writer.zig").Writer;
const Packet = @import("../packets/set_time.zig").Packet;
pub fn decode(r: *Reader) !Packet {
    return .{ .time = try r.readVarI32() };
}
pub fn encode(w: *Writer, p: Packet) !void {
    try w.writeVarI32(p.time);
}
