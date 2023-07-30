namespace Mpdfin.Mpd;

partial class CommandHandler
{
    public static Response TagTypes()
    {
        Response response = new();

        foreach (var tag in Enum.GetValues<Tag>())
        {
            response.Add("tagtype"u8, tag.ToString());
        }

        return response;
    }
}