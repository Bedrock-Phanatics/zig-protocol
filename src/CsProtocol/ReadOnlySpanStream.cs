using System;
using System.IO;

namespace CsProtocol;

internal sealed class ReadOnlySpanStream(ReadOnlyMemory<byte> memory) : Stream
{
    private readonly ReadOnlyMemory<byte> _memory = memory;
    private int _position;
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _memory.Length;
    public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }
    public override int Read(Span<byte> buffer)
    {
        int count = Math.Min(buffer.Length, _memory.Length - _position);
        _memory.Span.Slice(_position, count).CopyTo(buffer);
        _position += count;
        return count;
    }
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    public override int ReadByte() => _position == _memory.Length ? -1 : _memory.Span[_position++];
    public override long Seek(long offset, SeekOrigin origin)
    {
        long value = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => _position + offset, SeekOrigin.End => _memory.Length + offset, _ => throw new ArgumentOutOfRangeException(nameof(origin)) };
        if ((ulong)value > (ulong)_memory.Length) throw new IOException("Seek is outside the stream.");
        return _position = (int)value;
    }
    public override void Flush()
    {
        // NOOP
    }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
