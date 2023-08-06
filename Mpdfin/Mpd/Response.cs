using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Mpdfin.Mpd;

readonly record struct Response
{
    public ArrayBufferWriter<byte> Contents { get; init; }

    [SetsRequiredMembers]
    public Response() => Contents = new();

    [SetsRequiredMembers]
    public Response(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        Contents = new();
        Add(key, value);
    }

    [SetsRequiredMembers]
    public Response(ReadOnlySpan<byte> key, string value)
    {
        Contents = new();
        Add(key, value);
    }

    public void Add(ReadOnlySpan<byte> key, string? value)
    {
        if (value is not null)
        {
            Add(key, value.ToU8String());
        }
    }

    public void Add(ReadOnlySpan<byte> key, IReadOnlyList<string>? values)
    {
        if (values is not null)
        {
            foreach (var value in values)
            {
                Add(key, value.ToU8String());
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
