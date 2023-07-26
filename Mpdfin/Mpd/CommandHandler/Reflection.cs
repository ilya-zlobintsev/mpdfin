namespace Mpdfin.Mpd;

partial class CommandHandler
{
    static Response Commands()
    {
        Response response = new();

        foreach (var command in Enum.GetValues<Command>())
        {
            response.Add("command"u8, command.ToString());
        }

        return response;
    }

    static Response Decoders()
    {
        return new();
    }
}