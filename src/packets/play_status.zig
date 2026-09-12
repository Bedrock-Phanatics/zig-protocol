const Protocol = @import("../protocol.zig").Protocol;
const PacketId = @import("../registry/generated_packet_id.zig").PacketId;

pub const PlayStatus = enum(i32) {
    login_success = 0,
    login_failed_client = 1,
    login_failed_server = 2,
    player_spawn = 3,
    login_failed_invalid_tenant = 4,
    login_failed_vanilla_education = 5,
    login_failed_education_vanilla = 6,
    login_failed_server_full = 7,
    login_failed_editor_vanilla = 8,
    login_failed_vanilla_editor = 9,
};

pub const Current = struct {
    pub const id: PacketId = .play_status;
    status: PlayStatus,
};

pub fn Shape(comptime protocol: Protocol) type {
    _ = protocol;
    return Current;
}
