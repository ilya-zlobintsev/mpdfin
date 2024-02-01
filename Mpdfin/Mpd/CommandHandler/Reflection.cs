using DistIL.Attributes;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    [Optimize]
    static Response Commands()
    {
        return U8Enum
            .GetNames<Command>()
            .Aggregate(new Response(), (r, n) => r.Append("command"u8, n));
    }

    static Response Decoders() => new();
}