const Reader = @import("../codec/reader.zig").Reader;
const Writer = @import("../codec/writer.zig").Writer;
const packets = @import("../packets/login.zig");
const Protocol = @import("../protocol.zig").Protocol;

pub fn decode(comptime protocol: Protocol, r: *Reader) !packets.Shape(protocol) {
    return switch (protocol) {
        .Current => .{
            .protocol_version = try r.readI32Be(),
            .connection_request = try r.readByteArray(),
        },
    };
}

pub fn encode(comptime protocol: Protocol, w: *Writer, p: packets.Shape(protocol)) !void {
    switch (protocol) {
        .Current => {
            try w.writeI32Be(p.protocol_version);
            try w.writeByteArray(p.connection_request);
        },
    }
}
