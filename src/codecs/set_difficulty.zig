const Reader = @import("../codec/reader.zig").Reader;
const Writer = @import("../codec/writer.zig").Writer;
const Packet = @import("../packets/set_difficulty.zig").Packet;
pub fn decode(r: *Reader) !Packet {
    return .{ .difficulty = try r.readVarU32() };
}
pub fn encode(w: *Writer, p: Packet) !void {
    try w.writeVarU32(p.difficulty);
}
