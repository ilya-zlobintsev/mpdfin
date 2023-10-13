namespace Mpdfin.Mpd;

partial class CommandHandler
{
    Response Outputs()
    {
        Response response = new();

        int i = 0;
        foreach (var device in Player.AudioOutputDevices)
        {
            response.Add("outputid"u8, i.ToString());
            response.Add("outputname"u8, device.Name);
            response.Add("outputenabled"u8, "1"u8);

            i++;
        }

        return new();
    }
}