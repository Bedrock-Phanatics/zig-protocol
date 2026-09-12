const Protocol = @import("../protocol.zig").Protocol;
const PacketId = @import("../registry/generated_packet_id.zig").PacketId;

pub const Current = struct {
    pub const id: PacketId = .server_to_client_handshake;
    jwt: []const u8,
};

pub fn Shape(comptime protocol: Protocol) type {
    _ = protocol;
    return Current;
}
