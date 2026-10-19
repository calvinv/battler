using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Battler.Net;

/// <summary>
/// Reliable, ordered messages to the other player. TCP for LAN now; a Steam relay connection can
/// implement the same thing later without the session or game changing.
/// </summary>
public interface ITransport : IDisposable
{
    void Send(byte[] message);
    bool TryReceive(out byte[] message);
    bool Closed { get; }
}

/// <summary>Length-prefixed messages over a TCP socket; a background thread does the reading.</summary>
public sealed class TcpTransport : ITransport
{
    public const int DefaultPort = 27450;

    readonly TcpClient _client;
    readonly NetworkStream _stream;
    readonly ConcurrentQueue<byte[]> _inbox = new();
    readonly object _sendLock = new();
    volatile bool _closed;

    TcpTransport(TcpClient client)
    {
        _client = client;
        _client.NoDelay = true;   // lockstep messages are tiny and latency-bound
        _stream = client.GetStream();
        new Thread(ReadLoop) { IsBackground = true, Name = "net-read" }.Start();
    }

    public bool Closed => _closed;

    public static TcpListener Listen(int port = DefaultPort)
    {
        var l = new TcpListener(IPAddress.Any, port);
        l.Start();
        return l;
    }

    /// <summary>Non-blocking: the connection if someone is waiting, else null.</summary>
    public static TcpTransport TryAccept(TcpListener listener) =>
        listener.Pending() ? new TcpTransport(listener.AcceptTcpClient()) : null;

    public static async Task<TcpTransport> ConnectAsync(string host, int port = DefaultPort)
    {
        var c = new TcpClient();
        await c.ConnectAsync(host, port);
        return new TcpTransport(c);
    }

    public void Send(byte[] message)
    {
        if (_closed) return;
        try
        {
            var frame = new byte[4 + message.Length];
            BitConverter.TryWriteBytes(frame.AsSpan(0, 4), message.Length);
            message.CopyTo(frame, 4);
            lock (_sendLock) _stream.Write(frame, 0, frame.Length);
        }
        catch (Exception) { _closed = true; }
    }

    public bool TryReceive(out byte[] message) => _inbox.TryDequeue(out message);

    void ReadLoop()
    {
        try
        {
            var header = new byte[4];
            while (true)
            {
                ReadExactly(header);
                var len = BitConverter.ToInt32(header, 0);
                if (len < 0 || len > 1 << 20) throw new IOException("bad frame length");
                var body = new byte[len];
                ReadExactly(body);
                _inbox.Enqueue(body);
            }
        }
        catch (Exception) { _closed = true; }
    }

    void ReadExactly(byte[] buf)
    {
        int got = 0;
        while (got < buf.Length)
        {
            int n = _stream.Read(buf, got, buf.Length - got);
            if (n == 0) throw new EndOfStreamException();
            got += n;
        }
    }

    public void Dispose()
    {
        _closed = true;
        _client.Close();
    }
}
