const Reader = @import("../codec/reader.zig").Reader;
const Writer = @import("../codec/writer.zig").Writer;
const Packet = @import("../packets/client_to_server_handshake.zig").Packet;
pub fn decode(_: *Reader) !Packet {
    return .{};
}
pub fn encode(_: *Writer, _: Packet) !void {}
