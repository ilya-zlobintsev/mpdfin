using DistIL.Attributes;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    [Optimize]
    static Response TagTypes() => U8Enum
        .GetNames<Tag>()
        .Aggregate(new Response(), (response, tag) =>
        {
            response.Append("tagtype"u8, tag);
            return response;
        });
}