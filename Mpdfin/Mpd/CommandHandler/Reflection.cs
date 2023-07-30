namespace Mpdfin.Mpd;

partial class CommandHandler
{
    public static Response Commands()
    {
        Response response = new();

        foreach (var command in Enum.GetValues<Command>())
        {
            response.Add("command"u8, command.ToString());
        }

        return response;
    }

    public static Response Decoders()
    {
        return new();
    }
}