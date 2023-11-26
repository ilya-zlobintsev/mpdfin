using DistIL.Attributes;

namespace Mpdfin.Mpd;

partial class CommandHandler
{
    [Optimize]
    static Response TagTypes() => Constants
        .TagNames
        .Aggregate(new Response(), (response, tag) =>
        {
            response.Add("tagtype"u8, tag);
            return response;
        });
}