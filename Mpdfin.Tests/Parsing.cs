using Mpdfin.Mpd;

namespace Mpdfin.Tests;

public class Parsing
{
    [Fact]
    public void Basic()
    {
        Request request = new(u8("status"));
        Assert.Equal(Command.status, request.Command);
        Assert.Empty(request.Args);
    }

    [Fact]
    public void Arguments()
    {
        Request request = new(u8("playlistinfo 0 10"));
        Assert.Equal(Command.playlistinfo, request.Command);
        Assert.Equal([u8("0"), u8("10")], request.Args);
    }

    [Fact]
    public void QuotedArgument()
    {
        Request request = new(u8("pause \"1\""));
        Assert.Equal(Command.pause, request.Command);
        Assert.Equal([u8("1")], request.Args);
    }

    [Fact]
    public void QuotedArguments()
    {
        Request request = new(u8("seek \"0\" \"10\""));
        Assert.Equal(Command.seek, request.Command);
        Assert.Equal([u8("0"), u8("10")], request.Args);
    }

    [Fact]
    public void QuotedWithSpace()
    {
        Request request = new(u8("find \"multi word arg\""));
        Assert.Equal(Command.find, request.Command);
        Assert.Equal([u8("multi word arg")], request.Args);
    }

    [Fact]
    public void EscapedQuotesInArgument()
    {
        Request request = new(u8("find \"multi \\\"word\\\" arg\""));
        Assert.Equal(Command.find, request.Command);
        Assert.Equal([u8("multi \"word\" arg")], request.Args);
    }
}