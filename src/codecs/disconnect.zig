const Reader = @import("../codec/reader.zig").Reader;
const Writer = @import("../codec/writer.zig").Writer;
const Packet = @import("../packets/disconnect.zig").Packet;
pub fn decode(r: *Reader) !Packet {
    const reason = try r.readVarI32();
    const skipped = (try r.readVarU32()) != 0;
    if (skipped) return .{ .reason = reason, .message_skipped = true };
    return .{ .reason = reason, .message_skipped = false, .message = try r.readString(), .filtered_message = try r.readString() };
}
pub fn encode(w: *Writer, p: Packet) !void {
    try w.writeVarI32(p.reason);
    try w.writeVarU32(@intFromBool(p.message_skipped));
    if (!p.message_skipped) {
        try w.writeString(p.message);
        try w.writeString(p.filtered_message);
    }
}
