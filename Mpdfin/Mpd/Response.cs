using System.Buffers;
using System.Text;

namespace Mpdfin.Mpd;

readonly record struct Response
{
    public ArrayBufferWriter<byte> Contents { get; } = new();

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
        Contents.Write(key);
        Contents.Write(": "u8);
        Contents.Write(value);
        Contents.Write("\n"u8);
    }

    public readonly void AddListOk()
    {
        Contents.Write("list_OK\n"u8);
    }

    public void Extend(Response other)
    {
        Contents.Write(other.Contents.WrittenSpan);
    }

    public ReadOnlyMemory<byte> GetMemory()
    {
        return Contents.WrittenMemory;
    }

    public override string ToString()
    {
        return Encoding.UTF8.GetString(Contents.WrittenMemory.Span).Replace("\n", "; ");
    }
}
