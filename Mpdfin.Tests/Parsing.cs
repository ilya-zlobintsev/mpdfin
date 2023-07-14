namespace Mpdfin.Tests;

public class Parsing
{
    [Fact]
    public void Basic()
    {
        Request request = new("pause 1");
        Assert.Equal(Command.pause, request.Command);
        Assert.Equal(new List<string>() { "1" }, request.Args);
    }

    // [Fact]
    // public void ArgumentInQuotes()
    // {
    //     Request request = new("pause \"1\"");
    //     Assert.Equal(Command.pause, request.Command);
    //     Assert.Equal(new List<string>() { "1" }, request.Args);
    // }
}