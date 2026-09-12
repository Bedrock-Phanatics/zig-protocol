const std = @import("std");

const Limits = @import("../codec/limits.zig").DecodeLimits;
const Reader = @import("../codec/reader.zig").Reader;
const Writer = @import("../codec/writer.zig").Writer;
const Header = @import("../packet.zig").Header;
const Protocol = @import("../protocol.zig").Protocol;
const PacketId = @import("generated_packet_id.zig").PacketId;

const packets = struct {
    pub const login = @import("../packets/login.zig");
    pub const play_status = @import("../packets/play_status.zig");
};

const codecs = struct {
    pub const login = @import("../codecs/login.zig");
    pub const play_status = @import("../codecs/play_status.zig");
};

pub const UnknownPacket = struct {
    packet_id: u10,
    raw: []const u8,
    known: bool,
};

fn isKnownProtocolId(packet_id: u10) bool {
    const e: PacketId = @enumFromInt(packet_id);
    return std.enums.tagName(PacketId, e) != null;
}

pub fn Packet(comptime protocol: Protocol) type {
    return union(enum) {
        login: packets.login.Shape(protocol),
        play_status: packets.play_status.Shape(protocol),
        unknown: UnknownPacket,

        pub fn id(self: @This()) u10 {
            return switch (self) {
                .unknown => |u| u.packet_id,
                inline else => |payload| @intFromEnum(@TypeOf(payload).id),
            };
        }
    };
}

pub fn Envelope(comptime protocol: Protocol) type {
    return struct { header: Header, packet: Packet(protocol) };
}

pub fn decode(comptime protocol: Protocol, input: []const u8, limits: Limits) !Envelope(protocol) {
    var r = try Reader.init(input, limits);
    const h = try Header.fromWire(try r.readVarU32());

    const PacketT = Packet(protocol);
    const value: PacketT = blk: {
        inline for (std.meta.fields(PacketT)) |field| {
            if (field.type == UnknownPacket) continue;
            if (h.packet_id == @intFromEnum(field.type.id)) {
                const codec = @field(codecs, field.name);
                break :blk @unionInit(PacketT, field.name, try codec.decode(protocol, &r));
            }
        }
        break :blk .{ .unknown = .{
            .packet_id = h.packet_id,
            .raw = r.readRemaining(),
            .known = isKnownProtocolId(h.packet_id),
        } };
    };

    try r.finish();
    return .{ .header = h, .packet = value };
}

pub fn encode(comptime protocol: Protocol, w: *Writer, e: Envelope(protocol)) !void {
    switch (e.packet) {
        .unknown => |u| {
            if (e.header.packet_id != u.packet_id) return error.InvalidPacketId;
            try w.writeVarU32(e.header.toWire());
            try w.writeRaw(u.raw);
        },
        inline else => |payload, tag| {
            if (e.header.packet_id != @intFromEnum(@TypeOf(payload).id)) return error.InvalidPacketId;
            try w.writeVarU32(e.header.toWire());
            const codec = @field(codecs, @tagName(tag));
            try codec.encode(protocol, w, payload);
        },
    }
}
