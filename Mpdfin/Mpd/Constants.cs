using System.Collections.Immutable;

namespace Mpdfin.Mpd;

static class Constants
{
    public static readonly ImmutableArray<string> CommandNames = [..Enum.GetNames<Command>()];

    public static readonly ImmutableArray<string> TagNames = [..Enum.GetNames<Tag>()];
    public static readonly ImmutableArray<Tag> TagValues = [..Enum.GetValues<Tag>()];
}