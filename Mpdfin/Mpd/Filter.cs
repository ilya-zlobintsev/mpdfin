using Jellyfin.Sdk.Generated.Models;

namespace Mpdfin.Mpd;

readonly record struct Filter(Tag Tag, U8String? Value)
{
    public static List<Filter> ParseFilters(List<U8String> args)
    {
        var filters = new List<Filter>(args.Count / 2);

        for (int i = 0; i + 1 < args.Count; i += 2)
        {
            var tag = U8Enum.Parse<Tag>(args[i], true);
            var value = args[i + 1];
            filters.Add(new Filter(tag, value));
        }

        return filters;
    }
}

static class FilterExtensions
{
    public static bool MatchesFilter(this BaseItemDto item, Filter filter)
    {
        var tagValues = item.GetTagValue(filter.Tag);

        return tagValues?.Contains(filter.Value!.Value) ?? filter.Value is null or [];
    }
}
