using System.Buffers;
using System.Text;

namespace Mpdfin.Mpd;

readonly record struct Response
{
    public ArrayBufferWriter<byte> Buffer { get; } = new();

    public ReadOnlyMemory<byte> Contents => Buffer.WrittenMemory;

    public Response() { }

    public Response(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        Add(key, value);
    }

    public Response(ReadOnlySpan<byte> key, string value)
    {
        Add(key, value);
    }

    public void Add(ReadOnlySpan<byte> key, string? value)
    {
        if (value is not null)
        {
            var data = Encoding.UTF8.GetBytes(value);
            Add(key, data);
        }
    }

    public void Add(ReadOnlySpan<byte> key, IReadOnlyList<string>? values)
    {
        if (values is not null)
        {
            foreach (var value in values)
            {
                Add(key, value);
            }
        }
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
