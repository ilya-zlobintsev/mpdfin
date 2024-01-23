namespace Mpdfin.Mpd;

partial class CommandHandler
{
    Response Outputs()
    {
        Response response = new();

        int i = 0;
        foreach (var device in Player.AudioOutputDevices)
        {
            response.Append("outputid"u8, i);
            response.Append("outputname"u8, u8(device.Name));
            response.Append("outputenabled"u8, "1"u8);

            i++;
        }

        return new();
    }
}