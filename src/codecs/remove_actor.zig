const Reader = @import("../codec/reader.zig").Reader;
const Writer = @import("../codec/writer.zig").Writer;
const Packet = @import("../packets/remove_actor.zig").Packet;
pub fn decode(r: *Reader) !Packet {
    return .{ .entity_unique_id = try r.readVarI64() };
}
pub fn encode(w: *Writer, p: Packet) !void {
    try w.writeVarI64(p.entity_unique_id);
}
