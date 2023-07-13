using System.Net.Sockets;
using System.Text;
using Serilog;

namespace Mpdfin;

class ClientStream
{
    readonly static ReadOnlyMemory<byte> GREETING = "OK MPD 0.23.5"u8.ToArray();
    readonly TcpClient TcpClient;
    readonly NetworkStream Stream;
    readonly StreamReader Reader;

    public ClientStream(TcpClient client)
    {
        Log.Debug($"Opening new connection from {client.Client.RemoteEndPoint}");
        TcpClient = client;
        Stream = client.GetStream();
        Reader = new(Stream, Encoding.UTF8);
    }

    public async Task Write(ReadOnlyMemory<byte> data)
    {
        if (data.Length > 0)
        {
            await Stream.WriteAsync(data);

            if (data.Span[^1] is not (byte)'\n')
            {
                Stream.WriteByte((byte)'\n');
            }
        }
    }

    public Task WriteGreeting()
    {
        return Write(GREETING);
    }

    public async Task WriteResponse(Response response)
    {
        Log.Debug($"Writing response {response}");
        await Write(response.GetMemory());
        await Write("OK"u8.ToArray());
    }

    public Task WriteError(Ack error, uint commandListNum = 0, string currentCommand = "", string messageText = "")
    {
        var line = $"ACK [{(int)error}@{commandListNum}] {{{currentCommand}}} {messageText}";
        return Write(Encoding.UTF8.GetBytes(line));
    }

    public async Task DisposeAsync()
    {
        Log.Debug($"Closing connection from {TcpClient.Client.RemoteEndPoint}");
        Reader.Dispose();
        await Stream.DisposeAsync();
        TcpClient.Dispose();
    }

    public async IAsyncEnumerable<Request> ReadCommands()
    {
        while (!Reader.EndOfStream)
        {
            var line = await Reader.ReadLineAsync();

            if (string.IsNullOrEmpty(line))
            {
                yield break;
            }

            var split = line.Split();

            if (split.Length != 0)
            {
                var rawCommand = split[0];

                // Make sure the command isn't a numeric representation of the enum
                if (!int.TryParse(rawCommand, out int _) && Enum.TryParse(rawCommand, false, out Command command))
                {
                    List<string> args = new();

                    for (int i = 1; i < split.Length; i++)
                    {
                        args.Add(split[i]);
                    }

                    yield return new(command, args);
                }
                else
                {
                    await WriteError(Ack.UNKNOWN, messageText: $"unknown command {rawCommand}");
                }
            }
        }
    }
}
