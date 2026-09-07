pub const RequestNetworkSettingsPacket = struct { client_protocol: i32 };
pub const NetworkSettingsPacket = struct {
    compression_threshold: u16,
    compression_algorithm: u16,
    client_throttle: bool,
    client_throttle_threshold: u8,
    client_throttle_scalar: f32,
};
