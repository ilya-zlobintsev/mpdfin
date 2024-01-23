using DistIL.Attributes;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    static Response Commands()
    {
        var response = new Response();
        foreach (var name in U8Enum.GetNames<Command>())
        {
            response.Append("command"u8, name);
        }
        return response;
    }

    static Response Decoders() => new();
}