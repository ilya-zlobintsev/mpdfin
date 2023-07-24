using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using Serilog;

namespace Mpdfin.Mpd;

class ClientStream
{
    readonly static ReadOnlyMemory<byte> GREETING = "OK MPD 0.19.0"u8.ToArray();
    readonly TcpClient TcpClient;
    readonly NetworkStream Stream;
    readonly StreamReader Reader;
    public bool EndOfStream { get; private set; }

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

    public async IAsyncEnumerable<Request> ReadCommands([EnumeratorCancellation] CancellationToken ct = default)
    {
        while (true)
        {
            var line = await Reader.ReadLineAsync(ct);

            if (string.IsNullOrEmpty(line))
            {
                Log.Debug("Got EOF, closing client stream");
                EndOfStream = true;
                yield break;
            }

            Log.Debug($"Read client line {line}");

            Request request;

            try
            {
                request = new(line);
            }
            catch (Exception ex)
            {
                await WriteError(Ack.UNKNOWN, messageText: ex.Message);
                continue;
            }

            yield return request;
        }
    }
}
