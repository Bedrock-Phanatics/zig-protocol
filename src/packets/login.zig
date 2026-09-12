const PacketId = @import("../registry/generated_packet_id.zig").PacketId;
const Protocol = @import("../protocol.zig").Protocol;

pub const Current = struct {
    pub const id: PacketId = .login;
    protocol_version: i32,
    connection_request: []const u8,
};

pub fn Shape(comptime protocol: Protocol) type {
    _ = protocol;
    return Current;
}
