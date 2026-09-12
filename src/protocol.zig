pub const Protocol = enum(i32) {
    Current = 2169,

    pub fn version(self: Protocol) i32 {
        return @intFromEnum(self);
    }

    pub fn minecraftVersion() []u8 {
        return switch (Protocol) {
            .Current => "1.26.45",
        };
    }
};
