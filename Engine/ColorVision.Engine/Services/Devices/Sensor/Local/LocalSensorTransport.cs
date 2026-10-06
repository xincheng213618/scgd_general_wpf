using System;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Sensor.Local;

internal interface ILocalSensorTransport : IDisposable
{
    Task OpenAsync(LocalSensorConnectionConfig config, CancellationToken token);
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token);
    ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token);
}

internal sealed class LocalSensorTcpTransport : ILocalSensorTransport
{
    private readonly TcpClient client = new() { NoDelay = true };
    public async Task OpenAsync(LocalSensorConnectionConfig config, CancellationToken token) => await client.ConnectAsync(config.Addr.Trim(), config.Port, token).ConfigureAwait(false);
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token) => client.GetStream().ReadAsync(buffer, token);
    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token) => client.GetStream().WriteAsync(bytes, token);
    public void Dispose() => client.Dispose();
}

internal sealed class LocalSensorSerialTransport : ILocalSensorTransport
{
    private readonly SerialPort port = new();
    private readonly Channel<byte[]> received = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });
    private byte[]? remaining;
    private int offset;
    private int disposed;

    public async Task OpenAsync(LocalSensorConnectionConfig config, CancellationToken token)
    {
        port.PortName = config.Addr.Trim();
        port.BaudRate = config.Port;
        port.DataBits = config.DataBits;
        port.Parity = config.Parity;
        port.StopBits = config.StopBits;
        port.DtrEnable = config.DtrEnable;
        port.RtsEnable = config.RtsEnable;
        port.WriteTimeout = SerialPort.InfiniteTimeout;
        port.DataReceived += DataReceived;
        port.ErrorReceived += ErrorReceived;
        using var registration = token.Register(Dispose);
        await Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            try { port.Open(); token.ThrowIfCancellationRequested(); }
            finally
            {
                // A driver may finish Open after cancellation has already disposed the port.
                if (token.IsCancellationRequested || Volatile.Read(ref disposed) != 0) port.Dispose();
            }
        }, token).ConfigureAwait(false);
    }

    private void DataReceived(object sender, SerialDataReceivedEventArgs args)
    {
        try
        {
            int available = port.BytesToRead;
            if (available <= 0) return;
            if (available > LocalSensorSession.MaxResponseBytes) throw new IOException("串口接收数据超过缓冲区限制。");
            byte[] bytes = new byte[available];
            int count = port.Read(bytes, 0, bytes.Length);
            if (count <= 0) return;
            if (count != bytes.Length) Array.Resize(ref bytes, count);
            if (!received.Writer.TryWrite(bytes)) throw new IOException("串口接收缓冲区已满。");
        }
        catch (Exception ex) { received.Writer.TryComplete(ex); }
    }

    private void ErrorReceived(object sender, SerialErrorReceivedEventArgs args) => received.Writer.TryComplete(new IOException($"串口通信错误：{args.EventType}"));

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
    {
        if (remaining == null)
        {
            if (!await received.Reader.WaitToReadAsync(token).ConfigureAwait(false)) return 0;
            remaining = await received.Reader.ReadAsync(token).ConfigureAwait(false);
            offset = 0;
        }
        int count = Math.Min(buffer.Length, remaining.Length - offset);
        remaining.AsMemory(offset, count).CopyTo(buffer);
        offset += count;
        if (offset == remaining.Length) remaining = null;
        return count;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        using var registration = token.Register(Dispose);
        byte[] copy = bytes.ToArray();
        await Task.Run(() => { token.ThrowIfCancellationRequested(); port.Write(copy, 0, copy.Length); token.ThrowIfCancellationRequested(); }, token).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        port.DataReceived -= DataReceived;
        port.ErrorReceived -= ErrorReceived;
        received.Writer.TryComplete();
        port.Dispose();
    }
}
