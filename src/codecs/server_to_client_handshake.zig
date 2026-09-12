const Reader = @import("../codec/reader.zig").Reader;
const Writer = @import("../codec/writer.zig").Writer;
const packet = @import("../packets/server_to_client_handshake.zig");
const Protocol = @import("../protocol.zig").Protocol;

pub fn decode(comptime protocol: Protocol, r: *Reader) !packet.Shape(protocol) {
    return .{ .jwt = try r.readByteArray() };
}

pub fn encode(comptime protocol: Protocol, w: *Writer, p: packet.Shape(protocol)) !void {
    try w.writeByteArray(p.jwt);
}
