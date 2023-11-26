using DistIL.Attributes;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    [Optimize]
    static Response Commands() => Constants
        .CommandNames
        .Aggregate(new Response(), (response, command) =>
        {
            response.Add("command"u8, command);
            return response;
        });

    static Response Decoders()
    {
        return new();
    }
}