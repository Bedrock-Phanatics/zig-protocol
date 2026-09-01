using System;

namespace CsProtocol;

/// <summary>Resource limits applied to all untrusted network input.</summary>
public sealed record ProtocolLimits
{
    public static ProtocolLimits Default { get; } = new();

    public int MaxBatchBytes { get; init; } = 4 * 1024 * 1024;
    public int MaxDecompressedBatchBytes { get; init; } = 16 * 1024 * 1024;
    public int MaxPacketsPerBatch { get; init; } = 1024;
    public int MaxPacketBytes { get; init; } = 4 * 1024 * 1024;
    public int MaxStringBytes { get; init; } = 1 * 1024 * 1024;
    public int MaxByteArrayBytes { get; init; } = 8 * 1024 * 1024;
    public int MaxCollectionElements { get; init; } = 1_000_000;

    internal void Validate()
    {
        ValidatePositive(MaxBatchBytes, nameof(MaxBatchBytes));
        ValidatePositive(MaxDecompressedBatchBytes, nameof(MaxDecompressedBatchBytes));
        ValidatePositive(MaxPacketsPerBatch, nameof(MaxPacketsPerBatch));
        ValidatePositive(MaxPacketBytes, nameof(MaxPacketBytes));
        ValidatePositive(MaxStringBytes, nameof(MaxStringBytes));
        ValidatePositive(MaxByteArrayBytes, nameof(MaxByteArrayBytes));
        ValidatePositive(MaxCollectionElements, nameof(MaxCollectionElements));
    }

    private static void ValidatePositive(int value, string name)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(name, value, "Limit must be positive.");
    }
}
