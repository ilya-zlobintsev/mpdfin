using Serilog;

namespace Mpdfin.MediaKeys;

public class MediaKeysController
{
    public static async Task Init(Player.Player player)
    {
#if OS_LINUX
        Tmds.DBus.Connection connection = new(Tmds.DBus.Address.Session);

        string interfaceName = "org.mpris.MediaPlayer2.mpdfin";

        Linux.MediaPlayer2 dbusMediaPlayer2 = new();
        Linux.Player dbusPlayer = new(player);

        await connection.ConnectAsync();
        await connection.RegisterObjectAsync(dbusMediaPlayer2);
        await connection.RegisterObjectAsync(dbusPlayer);
        await connection.RegisterServiceAsync(interfaceName, Tmds.DBus.ServiceRegistrationOptions.ReplaceExisting);
#else
        Log.Warning("Media keys are not supported on the current OS");
#endif
    }
}
