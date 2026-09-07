const Reader = @import("../codec/reader.zig").Reader;
const Writer = @import("../codec/writer.zig").Writer;
const Packet = @import("../packets/server_to_client_handshake.zig").Packet;
pub fn decode(r: *Reader) !Packet {
    return .{ .jwt = try r.readByteArray() };
}
pub fn encode(w: *Writer, p: Packet) !void {
    try w.writeByteArray(p.jwt);
}
