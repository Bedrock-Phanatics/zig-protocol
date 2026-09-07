pub const Packet = struct { reason: i32, message_skipped: bool, message: []const u8 = "", filtered_message: []const u8 = "" };
