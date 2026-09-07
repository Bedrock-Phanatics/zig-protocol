const std = @import("std");
const Reader = @import("../codec/reader.zig").Reader;
const Writer = @import("../codec/writer.zig").Writer;
const packet = @import("../packets/play_status.zig");

pub fn decode(reader: *Reader) !packet.PlayStatusPacket {
    const raw = try reader.readI32Be();
    return .{ .status = (std.enums.fromInt(packet.PlayStatus, raw) orelse return error.InvalidEnum) };
}

pub fn encode(writer: *Writer, value: packet.PlayStatusPacket) !void {
    try writer.writeI32Be(@intFromEnum(value.status));
}
