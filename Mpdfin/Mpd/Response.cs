using System.Buffers;
using System.Text;

namespace Mpdfin.Mpd;

readonly record struct Response
{
    public ArrayBufferWriter<byte> Buffer { get; } = new();

    public ReadOnlyMemory<byte> Contents => Buffer.WrittenMemory;

    public Response(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        Add(key, value);
    }

    public void Add(ReadOnlySpan<byte> key, ICollection<U8String>? values)
    {
        if (values is not null)
        {
            foreach (var value in values)
            {
                Add(key, value);
            }
        }
    }

    public void Add<T>(ReadOnlySpan<byte> key, T value)
        where T : IUtf8SpanFormattable
    {
        Buffer.Write(key);
        Buffer.Write(": "u8);

        var length = 32;
    Retry:
        var destination = Buffer.GetSpan(length);
        if (value.TryFormat(destination, out var written, default, null))
        {
            Buffer.Advance(written);
            Buffer.Write("\n"u8);
            return;
        }

        length *= 2;
        goto Retry;
    }

    public void Add(ReadOnlySpan<byte> key, U8String value)
    {
        Add(key, value.AsSpan());
    }

    public void Add(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        Buffer.Write(key);
        Buffer.Write(": "u8);
        Buffer.Write(value);
        Buffer.Write("\n"u8);
    }

    public readonly void AddListOk()
    {
        Buffer.Write("list_OK\n"u8);
    }

    public Response Extend(Response other)
    {
        Buffer.Write(other.Buffer.WrittenSpan);
        return this;
    }

    public ReadOnlyMemory<byte> GetMemory()
    {
        return Buffer.WrittenMemory;
    }

    public override string ToString()
    {
        return Encoding.UTF8.GetString(Buffer.WrittenMemory.Span).Replace("\n", "; ");
    }
}
