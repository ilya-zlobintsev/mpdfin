namespace Mpdfin;

public enum Subsystem
{
    database,
    update,
    stored_playlist,
    playlist,
    player,
    mixer,
    output,
    options,
    partition,
    sticker,
    subscription,
    message,
    neighbor,
    mount,
}

public class SubsystemEventArgs : EventArgs
{
    public readonly Subsystem Subsystem;

    public SubsystemEventArgs(Subsystem subsystem)
    {
        Subsystem = subsystem;
    }
}
