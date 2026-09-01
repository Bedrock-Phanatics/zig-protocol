using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace CsProtocol;

public enum CompressionAlgorithm : byte
{
    Deflate = 0,
    Snappy = 1,
    None = 0xff
}

public sealed record BatchCodecOptions
{
    public ProtocolLimits Limits { get; init; } = ProtocolLimits.Default;
    public CompressionAlgorithm? Compression { get; init; }
    public int CompressionThreshold { get; init; } = 256;
    public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Fastest;
}

/// <summary>Encodes and decodes Bedrock 0xfe packet batches with bounded raw DEFLATE support.</summary>
public sealed class BatchCodec
{
    public const byte Header = 0xfe;
    private readonly BatchCodecOptions _options;
    private readonly BatchEncryption? _encryption;

    public BatchCodec(BatchCodecOptions? options = null, BatchEncryption? encryption = null)
    {
        _options = options ?? new BatchCodecOptions();
        _options.Limits.Validate();
        if (_options.CompressionThreshold < 0) throw new ArgumentOutOfRangeException(nameof(options), "Compression threshold cannot be negative.");
        if (_options.Compression is not null and not CompressionAlgorithm.Deflate and not CompressionAlgorithm.Snappy) throw new ArgumentOutOfRangeException(nameof(options), "Compression must be DEFLATE, Snappy, or disabled.");
        _encryption = encryption;
    }

    public byte[] Encode(IReadOnlyList<ReadOnlyMemory<byte>> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);
        if (packets.Count > _options.Limits.MaxPacketsPerBatch) throw new ProtocolException($"Packet count {packets.Count} exceeds limit {_options.Limits.MaxPacketsPerBatch}.");
        var body = new ArrayBufferWriter<byte>();
        var writer = new PacketWriter(body);
        foreach (ReadOnlyMemory<byte> packet in packets)
        {
            if (packet.IsEmpty) throw new ProtocolException("A batch cannot contain an empty packet.");
            if (packet.Length > _options.Limits.MaxPacketBytes) throw new ProtocolException($"Packet length {packet.Length} exceeds limit {_options.Limits.MaxPacketBytes}.");
            writer.WriteVarUInt32((uint)packet.Length);
            writer.WriteRaw(packet.Span);
            if (body.WrittenCount > _options.Limits.MaxDecompressedBatchBytes) throw new ProtocolException("Uncompressed batch exceeds configured limit.");
        }

        byte[] content;
        if (_options.Compression is null && _encryption is null)
        {
            return Frame(body.WrittenSpan);
        }
        else if (_options.Compression is null) content = body.WrittenSpan.ToArray();
        else if (body.WrittenCount < _options.CompressionThreshold)
        {
            content = new byte[body.WrittenCount + 1];
            content[0] = (byte)CompressionAlgorithm.None;
            body.WrittenSpan.CopyTo(content.AsSpan(1));
        }
        else if (_options.Compression == CompressionAlgorithm.Deflate)
        {
            using var output = new MemoryStream(Math.Min(body.WrittenCount, 64 * 1024));
            output.WriteByte((byte)CompressionAlgorithm.Deflate);
            using (var compressor = new DeflateStream(output, _options.CompressionLevel, leaveOpen: true)) compressor.Write(body.WrittenSpan);
            content = output.ToArray();
        }
        else
        {
            throw new NotSupportedException("Snappy compression requires an explicit codec and is not built into .NET.");
        }

        if (_encryption is not null)
        {
            if ((long)content.Length + BatchEncryption.ChecksumLength + 1 > _options.Limits.MaxBatchBytes) throw new ProtocolException("Encrypted batch exceeds configured limit.");
            content = _encryption.Encrypt(content);
        }
        return Frame(content);
    }

    private byte[] Frame(ReadOnlySpan<byte> content)
    {
        if ((long)content.Length + 1 > _options.Limits.MaxBatchBytes) throw new ProtocolException($"Final batch length including header {(long)content.Length + 1} exceeds limit {_options.Limits.MaxBatchBytes}.");
        byte[] result = new byte[content.Length + 1];
        result[0] = Header;
        content.CopyTo(result.AsSpan(1));
        return result;
    }

    public IReadOnlyList<ReadOnlyMemory<byte>> Decode(ReadOnlyMemory<byte> batch)
    {
        if (batch.Length > _options.Limits.MaxBatchBytes) throw new ProtocolException($"Batch length {batch.Length} exceeds limit {_options.Limits.MaxBatchBytes}.");
        if (batch.IsEmpty || batch.Span[0] != Header) throw new ProtocolException("Invalid or missing Bedrock batch header 0xfe.");
        ReadOnlyMemory<byte> content = batch[1..];
        if (_encryption is not null) content = _encryption.DecryptAndVerify(content.Span);

        ReadOnlyMemory<byte> body;
        if (_options.Compression is null)
        {
            body = content;
        }
        else
        {
            if (content.IsEmpty) throw new ProtocolException("Compressed batch has no compression algorithm byte.");
            CompressionAlgorithm algorithm = (CompressionAlgorithm)content.Span[0];
            ReadOnlyMemory<byte> compressed = content[1..];
            if (algorithm == CompressionAlgorithm.None)
            {
                if (compressed.Length > _options.Limits.MaxDecompressedBatchBytes) throw new ProtocolException("Uncompressed batch exceeds configured decompressed limit.");
                body = compressed;
            }
            else if (algorithm == CompressionAlgorithm.Deflate && _options.Compression == CompressionAlgorithm.Deflate)
            {
                body = DecompressDeflate(compressed);
            }
            else if (algorithm == CompressionAlgorithm.Snappy)
            {
                throw new NotSupportedException("Snappy-compressed Bedrock batches are not supported by the built-in codec.");
            }
            else
            {
                throw new ProtocolException($"Unexpected compression algorithm byte {(byte)algorithm}.");
            }
        }

        var packets = new List<ReadOnlyMemory<byte>>(Math.Min(16, _options.Limits.MaxPacketsPerBatch));
        int offset = 0;
        while (offset < body.Length)
        {
            var reader = new PacketReader(body.Span[offset..], _options.Limits, limitsValidated: true);
            uint rawLength = reader.ReadVarUInt32();
            int prefixLength = body.Length - offset - reader.Remaining;
            if (rawLength == 0) throw new ProtocolException("Batch contains an empty packet.");
            if (rawLength > (uint)_options.Limits.MaxPacketBytes || rawLength > (uint)reader.Remaining) throw new ProtocolException($"Packet length {rawLength} is invalid for {reader.Remaining} remaining byte(s).");
            if (packets.Count == _options.Limits.MaxPacketsPerBatch) throw new ProtocolException("Batch packet count exceeds configured limit.");
            packets.Add(body.Slice(offset + prefixLength, (int)rawLength));
            offset = checked(offset + prefixLength + (int)rawLength);
        }
        return packets;
    }

    private ReadOnlyMemory<byte> DecompressDeflate(ReadOnlyMemory<byte> compressed)
    {
        using var input = new ReadOnlySpanStream(compressed);
        using var decompressor = new DeflateStream(input, CompressionMode.Decompress);
        int initialCapacity = (int)Math.Min(Math.Max((long)compressed.Length * 2, 256), _options.Limits.MaxDecompressedBatchBytes);
        var output = new ArrayBufferWriter<byte>(initialCapacity);
        try
        {
            while (true)
            {
                int remaining = _options.Limits.MaxDecompressedBatchBytes - output.WrittenCount;
                if (remaining == 0)
                {
                    Span<byte> extra = stackalloc byte[1];
                    if (decompressor.Read(extra) != 0) throw new ProtocolException("Decompressed batch exceeds configured limit.");
                    break;
                }

                Span<byte> target = output.GetSpan(Math.Min(8192, remaining));
                int read = decompressor.Read(target[..Math.Min(target.Length, remaining)]);
                if (read == 0) break;
                output.Advance(read);
            }
        }
        catch (InvalidDataException exception)
        {
            throw new ProtocolException("Invalid DEFLATE batch.", exception);
        }
        return output.WrittenMemory.ToArray();
    }
}
