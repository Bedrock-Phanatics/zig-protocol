const Reader = @import("../codec/reader.zig").Reader;
const Writer = @import("../codec/writer.zig").Writer;
const Limits = @import("../codec/limits.zig").DecodeLimits;
const Header = @import("../packet.zig").Header;
pub const Packet = union(enum) {
    login: @import("../packets/login.zig").Packet,
    play_status: @import("../packets/play_status.zig").PlayStatusPacket,
    server_to_client_handshake: @import("../packets/server_to_client_handshake.zig").Packet,
    client_to_server_handshake: @import("../packets/client_to_server_handshake.zig").Packet,
    disconnect: @import("../packets/disconnect.zig").Packet,
    set_time: @import("../packets/set_time.zig").Packet,
    remove_actor: @import("../packets/remove_actor.zig").Packet,
    move_player: @import("../packets/move_player.zig").MovePlayerPacket,
    set_health: @import("../packets/set_health.zig").Packet,
    set_commands_enabled: @import("../packets/set_commands_enabled.zig").Packet,
    set_difficulty: @import("../packets/set_difficulty.zig").Packet,
    request_chunk_radius: @import("../packets/request_chunk_radius.zig").Packet,
    chunk_radius_updated: @import("../packets/chunk_radius_updated.zig").Packet,
    network_stack_latency: @import("../packets/network_stack_latency.zig").Packet,
    network_settings: @import("../packets/network_settings.zig").NetworkSettingsPacket,
    request_network_settings: @import("../packets/network_settings.zig").RequestNetworkSettingsPacket,
};
pub const Envelope = struct { header: Header, packet: Packet };
fn packetId(packet: Packet) u10 {
    return switch (packet) {
        .login => 1,
        .play_status => 2,
        .server_to_client_handshake => 3,
        .client_to_server_handshake => 4,
        .disconnect => 5,
        .set_time => 10,
        .remove_actor => 14,
        .move_player => 19,
        .set_health => 42,
        .set_commands_enabled => 59,
        .set_difficulty => 60,
        .request_chunk_radius => 69,
        .chunk_radius_updated => 70,
        .network_stack_latency => 115,
        .network_settings => 143,
        .request_network_settings => 193,
    };
}
pub fn decode(input: []const u8, limits: Limits) !Envelope {
    var r = try Reader.init(input, limits);
    const h = try Header.fromWire(try r.readVarU32());
    const value: Packet = switch (h.packet_id) {
        1 => .{ .login = try @import("../codecs/login.zig").decode(&r) },
        2 => .{ .play_status = try @import("../codecs/play_status.zig").decode(&r) },
        3 => .{ .server_to_client_handshake = try @import("../codecs/server_to_client_handshake.zig").decode(&r) },
        4 => .{ .client_to_server_handshake = try @import("../codecs/client_to_server_handshake.zig").decode(&r) },
        5 => .{ .disconnect = try @import("../codecs/disconnect.zig").decode(&r) },
        10 => .{ .set_time = try @import("../codecs/set_time.zig").decode(&r) },
        14 => .{ .remove_actor = try @import("../codecs/remove_actor.zig").decode(&r) },
        19 => .{ .move_player = try @import("../codecs/move_player.zig").decode(&r) },
        42 => .{ .set_health = try @import("../codecs/set_health.zig").decode(&r) },
        59 => .{ .set_commands_enabled = try @import("../codecs/set_commands_enabled.zig").decode(&r) },
        60 => .{ .set_difficulty = try @import("../codecs/set_difficulty.zig").decode(&r) },
        69 => .{ .request_chunk_radius = try @import("../codecs/request_chunk_radius.zig").decode(&r) },
        70 => .{ .chunk_radius_updated = try @import("../codecs/chunk_radius_updated.zig").decode(&r) },
        115 => .{ .network_stack_latency = try @import("../codecs/network_stack_latency.zig").decode(&r) },
        143 => .{ .network_settings = try @import("../codecs/network_settings.zig").decode(&r) },
        193 => .{ .request_network_settings = try @import("../codecs/network_settings.zig").decodeRequest(&r) },
        else => return error.InvalidPacketId,
    };
    try r.finish();
    return .{ .header = h, .packet = value };
}
pub fn encode(w: *Writer, e: Envelope) !void {
    if (e.header.packet_id != packetId(e.packet)) return error.InvalidValue;
    try w.writeVarU32(e.header.toWire());
    switch (e.packet) {
        .login => |v| {
            if (e.header.packet_id != 1) return error.InvalidValue;
            try @import("../codecs/login.zig").encode(w, v);
        },
        .play_status => |v| {
            if (e.header.packet_id != 2) return error.InvalidValue;
            try @import("../codecs/play_status.zig").encode(w, v);
        },
        .server_to_client_handshake => |v| {
            if (e.header.packet_id != 3) return error.InvalidValue;
            try @import("../codecs/server_to_client_handshake.zig").encode(w, v);
        },
        .client_to_server_handshake => |v| {
            if (e.header.packet_id != 4) return error.InvalidValue;
            try @import("../codecs/client_to_server_handshake.zig").encode(w, v);
        },
        .disconnect => |v| {
            if (e.header.packet_id != 5) return error.InvalidValue;
            try @import("../codecs/disconnect.zig").encode(w, v);
        },
        .set_time => |v| {
            if (e.header.packet_id != 10) return error.InvalidValue;
            try @import("../codecs/set_time.zig").encode(w, v);
        },
        .remove_actor => |v| {
            if (e.header.packet_id != 14) return error.InvalidValue;
            try @import("../codecs/remove_actor.zig").encode(w, v);
        },
        .move_player => |v| {
            if (e.header.packet_id != 19) return error.InvalidValue;
            try @import("../codecs/move_player.zig").encode(w, v);
        },
        .set_health => |v| {
            if (e.header.packet_id != 42) return error.InvalidValue;
            try @import("../codecs/set_health.zig").encode(w, v);
        },
        .set_commands_enabled => |v| {
            if (e.header.packet_id != 59) return error.InvalidValue;
            try @import("../codecs/set_commands_enabled.zig").encode(w, v);
        },
        .set_difficulty => |v| {
            if (e.header.packet_id != 60) return error.InvalidValue;
            try @import("../codecs/set_difficulty.zig").encode(w, v);
        },
        .request_chunk_radius => |v| {
            if (e.header.packet_id != 69) return error.InvalidValue;
            try @import("../codecs/request_chunk_radius.zig").encode(w, v);
        },
        .chunk_radius_updated => |v| {
            if (e.header.packet_id != 70) return error.InvalidValue;
            try @import("../codecs/chunk_radius_updated.zig").encode(w, v);
        },
        .network_stack_latency => |v| {
            if (e.header.packet_id != 115) return error.InvalidValue;
            try @import("../codecs/network_stack_latency.zig").encode(w, v);
        },
        .network_settings => |v| {
            if (e.header.packet_id != 143) return error.InvalidValue;
            try @import("../codecs/network_settings.zig").encode(w, v);
        },
        .request_network_settings => |v| {
            if (e.header.packet_id != 193) return error.InvalidValue;
            try @import("../codecs/network_settings.zig").encodeRequest(w, v);
        },
    }
}
