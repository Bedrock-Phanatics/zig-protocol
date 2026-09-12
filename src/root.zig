pub const batch = @import("codec/batch.zig");
pub const DecodeError = @import("codec/errors.zig").DecodeError;
pub const EncodeError = @import("codec/errors.zig").EncodeError;
pub const DecodeLimits = @import("codec/limits.zig").DecodeLimits;
pub const nbt = @import("codec/nbt.zig");
pub const Reader = @import("codec/reader.zig").Reader;
pub const Writer = @import("codec/writer.zig").Writer;
pub const codecs = @import("codecs/root.zig");
pub const packet = @import("packet.zig");
pub const packets = @import("packets/root.zig");
pub const Protocol = @import("protocol.zig").Protocol;
pub const PacketId = @import("registry/generated_packet_id.zig").PacketId;
pub const typed = @import("registry/typed.zig");
pub const BlockPosition = @import("types/block_position.zig").BlockPosition;
pub const Rgba = @import("types/colour.zig").Rgba;
pub const ChunkPosition = @import("types/position.zig").ChunkPosition;
pub const SubChunkPosition = @import("types/position.zig").SubChunkPosition;
pub const Vec2f = @import("types/vector.zig").Vec2f;
pub const Vec3f = @import("types/vector.zig").Vec3f;

test {
    _ = @import("tests/primitives.zig");
    _ = @import("tests/packet.zig");
    _ = @import("tests/audit.zig");
    _ = @import("tests/typed.zig");
    _ = @import("tests/nbt.zig");
    _ = @import("tests/compression.zig");
    _ = @import("tests/resource_pack.zig");
}
