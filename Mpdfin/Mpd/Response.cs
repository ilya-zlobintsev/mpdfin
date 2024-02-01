using System.Buffers;
using System.Globalization;
using System.Text;
using U8.Abstractions;

namespace Mpdfin.Mpd;

readonly record struct Response : IU8Formattable
{
    public ArrayBufferWriter<byte> Buffer { get; } = new();

    public ReadOnlyMemory<byte> Contents => Buffer.WrittenMemory;

    public Response() { }

    public Response(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        Append(key, value);
    }

    public Response Append(ReadOnlySpan<byte> key, ICollection<U8String>? values)
    {
        if (values is not null)
        {
            foreach (var value in values)
            {
                Append(key, value);
            }
        }

        return this;
    }

    public Response Append<T>(ReadOnlySpan<byte> key, T value)
        where T : IUtf8SpanFormattable
    {
        Buffer.Write(key);
        Buffer.Write(": "u8);

        var length = 32;
    Retry:
        var destination = Buffer.GetSpan(length);
        if (value.TryFormat(destination, out var written, default, CultureInfo.InvariantCulture))
        {
            Buffer.Advance(written);
            Buffer.Write("\n"u8);
            return this;
        }

        length *= 2;
        goto Retry;
    }

    public Response Append(ReadOnlySpan<byte> key, U8String value)
    {
        return Append(key, value.AsSpan());
    }

    public Response Append(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        Buffer.Write(key);
        Buffer.Write(": "u8);
        Buffer.Write(value);
        Buffer.Write("\n"u8);
        return this;
    }

    public readonly void AppendListOk()
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

    public U8String ToU8String(ReadOnlySpan<char> _ = default, IFormatProvider? __ = null)
    {
        return u8(Buffer.WrittenMemory.Span).Replace("\n"u8, "; "u8);
    }
}
