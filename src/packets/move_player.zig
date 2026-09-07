const Vec3f = @import("../types/vector.zig").Vec3f;
pub const MoveMode = enum(u8) { normal = 0, reset = 1, teleport = 2, rotation = 3 };
pub const TeleportData = struct { cause: i32, source_entity_type: i32 };
pub const MovePlayerPacket = struct {
    entity_runtime_id: u64,
    position: Vec3f,
    rotation: Vec3f,
    mode: MoveMode,
    on_ground: bool,
    ridden_entity_runtime_id: u64,
    teleport: ?TeleportData,
    tick: u64,
};
