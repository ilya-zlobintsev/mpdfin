using System.Collections.Immutable;

namespace Mpdfin.Mpd;

static class Constants
{
    public static readonly ImmutableArray<U8String> CommandNames = [..Enum.GetNames<Command>().Select(U8String.Create)];
    public static readonly ImmutableArray<U8String> TagNames = [..Enum.GetNames<Tag>().Select(U8String.Create)];
    public static readonly ImmutableArray<Tag> TagValues = [..Enum.GetValues<Tag>()];
}