const std = @import("std");
const Reader = @import("../codec/reader.zig").Reader;
const Writer = @import("../codec/writer.zig").Writer;
const Protocol = @import("../protocol.zig").Protocol;
const packet = @import("../packets/play_status.zig");

pub fn decode(comptime protocol: Protocol, reader: *Reader) !packet.Shape(protocol) {
    const raw = try reader.readI32Be();
    return .{ .status = (std.enums.fromInt(packet.PlayStatus, raw) orelse return error.InvalidEnum) };
}

pub fn encode(comptime protocol: Protocol, writer: *Writer, value: packet.Shape(protocol)) !void {
    try writer.writeI32Be(@intFromEnum(value.status));
}
