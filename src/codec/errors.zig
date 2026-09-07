pub const DecodeError = error{
    EndOfStream,
    VarIntOverflow,
    NonCanonicalVarInt,
    InvalidBoolean,
    InvalidUtf8,
    LimitExceeded,
    InvalidEnum,
    InvalidPacketId,
    InvalidCheckpoint,
    TrailingData,
};

pub const EncodeError = error{ NoSpaceLeft, LimitExceeded, InvalidValue };
