using System;
using System.IO;
using System.IO.Ports;

namespace ColorVision.Engine.Services.Devices.CfwPort.Local;

internal interface ILocalCfwPortTransport
{
    void Open(LocalCfwPortConnection config);
    void Close();
    void SetPort(int port);
    int ReadPort(int timeout);
}

internal sealed class LocalCfwPortTransport : ILocalCfwPortTransport
{
    private SerialPort? serialPort;

    public void Open(LocalCfwPortConnection config)
    {
        serialPort = CreateSerialPort(config);
        serialPort.Open();
    }

    // Match the standalone FilterWheel service's serial control lines.
    internal static SerialPort CreateSerialPort(LocalCfwPortConnection config) =>
        new(config.PortName, config.BaudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            DtrEnable = false,
            RtsEnable = false
        };

    public void Close()
    {
        serialPort?.Dispose();
        serialPort = null;
    }

    public void SetPort(int port)
    {
        serialPort!.DiscardInBuffer();
        // The wheel uses one byte for all positions: '0' plus the zero-based index.
        Exchange(serialPort.BaseStream, [(byte)('0' + port)], 10000);
    }

    public int ReadPort(int timeout)
    {
        serialPort!.DiscardInBuffer();
        return ReadPosition(serialPort.BaseStream, timeout);
    }

    internal static int ReadPosition(Stream stream, int timeout) => Exchange(stream, "NOW"u8, timeout) - '0';

    private static int Exchange(Stream stream, ReadOnlySpan<byte> command, int timeout)
    {
        stream.WriteTimeout = timeout;
        stream.ReadTimeout = timeout;
        stream.Write(command);
        int response = stream.ReadByte();
        if (response < 0) throw new EndOfStreamException("串口已断开。");
        return response;
    }
}
