namespace Umbraco.Automate.Web.Api.Webhook;

/// <summary>
/// A stream wrapper that limits the total bytes read. Throws <see cref="InvalidOperationException"/>
/// when the limit is exceeded. Used to prevent oversized webhook payloads from being read into memory.
/// </summary>
internal sealed class LimitedStream : Stream
{
    private readonly Stream _inner;
    private readonly long _maxBytes;
    private long _totalRead;

    public LimitedStream(Stream inner, long maxBytes)
    {
        _inner = inner;
        _maxBytes = maxBytes;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;
    public override long Position
    {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        _totalRead += read;

        if (_totalRead > _maxBytes)
        {
            throw new InvalidOperationException("Payload size limit exceeded.");
        }

        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var read = await _inner.ReadAsync(buffer, offset, count, cancellationToken);
        _totalRead += read;

        if (_totalRead > _maxBytes)
        {
            throw new InvalidOperationException("Payload size limit exceeded.");
        }

        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken);
        _totalRead += read;

        if (_totalRead > _maxBytes)
        {
            throw new InvalidOperationException("Payload size limit exceeded.");
        }

        return read;
    }

    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
