using System.Buffers;
using System.Collections.Immutable;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace Mpdfin.Mpd;

class ClientStream
{
    readonly static ImmutableArray<byte> GREETING = [.."OK MPD 0.19.0\n"u8];
    readonly static ImmutableArray<byte> OK = [.."OK\n"u8];
    readonly TcpClient TcpClient;
    readonly NetworkStream Stream;
    readonly StreamReader Reader;
    readonly SemaphoreSlim Lock;
    public bool EndOfStream { get; private set; }

    public ClientStream(TcpClient client)
    {
        Log.Debug($"Opening new connection from {client.Client.RemoteEndPoint}");
        TcpClient = client;
        Stream = client.GetStream();
        Reader = new(Stream, Encoding.UTF8);
        Lock = new(1, 1);
    }

    public Task WriteGreeting()
    {
        return Write(GREETING.AsMemory());
    }

    public async Task WriteResponse(Response response)
    {
        if (!response.Contents.IsEmpty)
        {
            var responseText = response.ToString();
            if (responseText.Length > 100)
            {
                responseText = $"{responseText.AsSpan(0, 100)}...";
            }

            Log.Debug($"Writing response {responseText}");
        }
        else
        {
            Log.Debug("Writing empty response");
        }

        response.Buffer.Write(OK.AsSpan());
        await Write(response.Contents);
    }

    public Task WriteError(Ack error, uint commandListNum = 0, string currentCommand = "", string messageText = "")
    {
        var line = $"ACK [{(int)error}@{commandListNum}] {{{currentCommand}}} {messageText}\n";
        return Write(Encoding.UTF8.GetBytes(line));
    }

    public async Task DisposeAsync()
    {
        Log.Debug($"Closing connection from {TcpClient.Client.RemoteEndPoint}");
        Reader.Dispose();
        await Stream.DisposeAsync();
        TcpClient.Dispose();
    }

    public async Task<Request?> ReadRequest(CancellationToken ct = default)
    {
        while (true)
        {
            string? line = null;
            using (await Lock.LockAsync())
            {
                line = await Reader.ReadLineAsync(ct);
            }

            if (string.IsNullOrEmpty(line))
            {
                Log.Debug("Got EOF, closing client stream");
                EndOfStream = true;
                return null;
            }

            Log.Debug($"Read client line {line}");

            try
            {
                return new(line);
            }
            catch (Exception ex)
            {
                await WriteError(Ack.UNKNOWN, messageText: ex.Message);
            }
        }
    }

    async Task Write(ReadOnlyMemory<byte> data)
    {
        using var streamLock = await Lock.LockAsync();
        if (data.Length > 0)
        {
            await Stream.WriteAsync(data);
        }
    }
}
