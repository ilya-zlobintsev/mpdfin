using System.Text;

namespace Mpdfin;

public static class Extensions
{
    public static string ToUtf16(this byte[] utf8) => Encoding.UTF8.GetString(utf8);
    public static string ToUtf16(this Memory<byte> utf8) => Encoding.UTF8.GetString(utf8.Span);
    public static string ToUtf16(this ReadOnlyMemory<byte> utf8) => Encoding.UTF8.GetString(utf8.Span);
}