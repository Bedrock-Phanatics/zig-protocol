using System;
using System.Buffers;
using System.IO;

namespace CsProtocol;

internal sealed class BufferWriterStream(IBufferWriter<byte> writer) : Stream
{
    private readonly IBufferWriter<byte> _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush()
    {
        // NOOP
    }
    public override void Write(ReadOnlySpan<byte> buffer) { while (!buffer.IsEmpty) { Span<byte> span = _writer.GetSpan(1); int count = Math.Min(span.Length, buffer.Length); buffer[..count].CopyTo(span); _writer.Advance(count); buffer = buffer[count..]; } }
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
    public override void WriteByte(byte value) { Span<byte> span = _writer.GetSpan(1); span[0] = value; _writer.Advance(1); }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
