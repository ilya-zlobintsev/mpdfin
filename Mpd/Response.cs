using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Serilog;

namespace Mpdfin;

readonly record struct Response
{
    ArrayBufferWriter<byte> Contents { get; init; }

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

    public void Add((string, string) kv)
    {
        Add(kv.Item1.ToU8String(), kv.Item2.ToU8String());
    }

    public void Add(ReadOnlySpan<byte> key, string value)
    {
        Add(key, value.ToU8String());
    }

    public void Add(ReadOnlySpan<byte> key, U8String value)
    {
        Add(key, value.AsSpan());
    }

    public void Add(ReadOnlySpan<byte> key, ReadOnlySpan<Byte> value)
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
        Log.Debug($"Combining with other response {other}");
        Contents.Write(other.Contents.WrittenSpan);
    }

    public ReadOnlyMemory<byte> GetMemory()
    {
        return Contents.WrittenMemory;
    }

    public override string ToString()
    {
        return Encoding.UTF8.GetString(Contents.WrittenMemory.Span).Replace("\n", " ");
    }
}
