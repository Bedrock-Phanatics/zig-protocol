using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace CsProtocol;

/// <summary>Connection-scoped Bedrock AES-256-CTR encryption and rolling SHA-256 authentication state.</summary>
/// <remarks>Instances are stateful and intentionally not thread-safe; serialize calls per connection direction.</remarks>
public sealed class BatchEncryption : IDisposable
{
    internal const int ChecksumLength = 8;
    private readonly byte[] _key;
    private readonly ICryptoTransform _aes;
    private readonly IncrementalHash _hash;
    private readonly byte[] _counterBlock = new byte[16];
    private readonly byte[] _keyStream = new byte[16];
    private int _keyStreamOffset = 16;
    private ulong _packetCounter;
    private bool _disposed;

    public BatchEncryption(ReadOnlySpan<byte> key)
    {
        if (key.Length != 32) throw new ArgumentException("Bedrock encryption requires a 32-byte key.", nameof(key));
        _key = key.ToArray();
        key[..12].CopyTo(_counterBlock);
        _counterBlock[15] = 2;
        Aes aes = Aes.Create();
        aes.Mode = CipherMode.ECB; aes.Padding = PaddingMode.None; aes.Key = _key;
        _aes = aes.CreateEncryptor();
        aes.Dispose();
        _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    }

    public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
    {
        ThrowIfDisposed();
        byte[] result = new byte[checked(plaintext.Length + ChecksumLength)];
        plaintext.CopyTo(result);
        ComputeChecksum(plaintext, result.AsSpan(plaintext.Length, ChecksumLength));
        Transform(result);
        return result;
    }

    public byte[] DecryptAndVerify(ReadOnlySpan<byte> ciphertext)
    {
        ThrowIfDisposed();
        if (ciphertext.Length < ChecksumLength) throw new ProtocolException("Encrypted batch is shorter than its checksum.");
        int payloadLength = ciphertext.Length - ChecksumLength;
        byte[] plaintext = new byte[payloadLength];
        ciphertext[..payloadLength].CopyTo(plaintext);
        Transform(plaintext);
        Span<byte> checksum = stackalloc byte[ChecksumLength];
        ciphertext[payloadLength..].CopyTo(checksum);
        Transform(checksum);
        Span<byte> expected = stackalloc byte[ChecksumLength];
        ComputeChecksum(plaintext, expected);
        if (!CryptographicOperations.FixedTimeEquals(expected, checksum)) throw new ProtocolException("Encrypted batch checksum is invalid.");
        return plaintext;
    }

    private void ComputeChecksum(ReadOnlySpan<byte> payload, Span<byte> destination)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(counter, _packetCounter++);
        _hash.AppendData(counter); _hash.AppendData(payload); _hash.AppendData(_key);
        Span<byte> digest = stackalloc byte[32];
        if (!_hash.TryGetHashAndReset(digest, out int written) || written != 32) throw new CryptographicException("Could not compute Bedrock batch checksum.");
        digest[..8].CopyTo(destination);
        CryptographicOperations.ZeroMemory(digest);
    }

    private void Transform(Span<byte> data)
    {
        int dataOffset = 0;
        while (dataOffset < data.Length)
        {
            if (_keyStreamOffset == 16)
            {
                _ = _aes.TransformBlock(_counterBlock, 0, 16, _keyStream, 0);
                IncrementCounterBigEndian(_counterBlock);
                _keyStreamOffset = 0;
            }

            int count = Math.Min(16 - _keyStreamOffset, data.Length - dataOffset);
            for (int i = 0; i < count; i++) data[dataOffset + i] ^= _keyStream[_keyStreamOffset + i];
            dataOffset += count;
            _keyStreamOffset += count;
        }
    }

    private static void IncrementCounterBigEndian(Span<byte> counter)
    {
        for (int i = counter.Length - 1; i >= 0; i--)
        {
            counter[i]++;
            if (counter[i] != 0) break;
        }
    }
    private void ThrowIfDisposed() { ObjectDisposedException.ThrowIf(_disposed, this); }
    public void Dispose() { if (_disposed) return; _disposed = true; _aes.Dispose(); _hash.Dispose(); CryptographicOperations.ZeroMemory(_key); CryptographicOperations.ZeroMemory(_counterBlock); CryptographicOperations.ZeroMemory(_keyStream); }
}
