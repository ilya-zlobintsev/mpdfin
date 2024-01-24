using System.Buffers;
using System.Net.Sockets;
using System.Text;
using Serilog;
using Serilog.Events;

namespace Mpdfin.Mpd;

class ClientStream : IAsyncDisposable
{
    static U8String GREETING => u8("OK MPD 0.19.0\n");
    static U8String OK => u8("OK\n");

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
        return Write(GREETING);
    }

    public Task WriteResponse(Response response)
    {
        if (!response.Contents.IsEmpty && Log.IsEnabled(LogEventLevel.Debug))
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

        response.Buffer.Write(OK);
        return Write(response.Contents);
    }

    public Task WriteError(Ack error, uint commandListNum = 0, string currentCommand = "", string messageText = "")
    {
        return Write(u8($"ACK [{(int)error}@{commandListNum}] {{{currentCommand.AsSpan()}}} {messageText.AsSpan()}\n"));
    }

    public async ValueTask DisposeAsync()
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
            var line = U8String.Empty;
            using (await Lock.LockAsync())
            {
                line = u8(await Reader.ReadLineAsync(ct) ?? "");
            }

            if (line.IsEmpty)
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
