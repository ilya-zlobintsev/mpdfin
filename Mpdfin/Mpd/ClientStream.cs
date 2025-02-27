using System.Buffers;
using System.Net.Sockets;
using Serilog;
using Serilog.Events;
using U8.IO;

namespace Mpdfin.Mpd;

class ClientStream : IAsyncDisposable
{
    static U8String GREETING => u8("OK MPD 0.19.0\n");
    static U8String OK => u8("OK\n");

    readonly TcpClient TcpClient;
    readonly NetworkStream Stream;
    readonly U8Reader<U8StreamSource> Reader;
    readonly SemaphoreSlim Lock;
    public bool EndOfStream { get; private set; }

    public ClientStream(TcpClient client)
    {
        Log.Debug($"Opening new connection from {client.Client.RemoteEndPoint}");
        TcpClient = client;
        Stream = client.GetStream();
        Reader = Stream.AsU8Reader(disposeSource: false);
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
        return Write($"ACK [{(int)error}@{commandListNum}] {{{currentCommand}}} {messageText}\n");
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
                line = await Reader.ReadLineAsync(ct) ?? U8String.Empty;
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

    async Task Write(PooledU8Builder data)
    {
        using var streamLock = await Lock.LockAsync();
        await Stream.WriteAsync(data);
    }
}
