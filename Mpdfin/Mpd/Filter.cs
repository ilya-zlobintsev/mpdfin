using Jellyfin.Sdk;
using Mpdfin.Mpd;

namespace Mpdfin.Mpd;

readonly record struct Filter(Tag Tag, string Value)
{
    public static List<Filter> ParseFilters(List<string> args)
    {
        List<Filter> filters = new();

        for (int i = 0; i + 1 < args.Count; i += 2)
        {
            var tag = Enum.Parse<Tag>(args[i]);
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

        return tagValues?.Any(tagValue => tagValue == filter.Value) == true;
    }
}