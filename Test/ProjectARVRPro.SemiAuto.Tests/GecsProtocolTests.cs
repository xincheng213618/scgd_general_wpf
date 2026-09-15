using ProjectARVRPro.SemiAuto.GECS;
using System.Text;

namespace ProjectARVRPro.SemiAuto.Tests;

public class GecsProtocolTests
{
    [Fact]
    public void BuildPacketUsesExcelFramingAndAsciiLength()
    {
        const string message = "PG,01,POWER,ON";

        byte[] packet = GecsPacketCodec.BuildPacket(message, 0xFF);

        Assert.Equal(0x02, packet[0]);
        Assert.Equal(0xFF, packet[1]);
        Assert.Equal(message.Length.ToString("X4"), Encoding.ASCII.GetString(packet, 2, 4));
        Assert.Equal(message, Encoding.ASCII.GetString(packet, 6, message.Length));
        Assert.Equal(0x03, packet[^1]);
    }

    [Fact]
    public void TryReadFrameHandlesNoiseFragmentationAndStickyFrames()
    {
        byte[] first = GecsPacketCodec.BuildPacket("ALIVE", 0xFF);
        byte[] second = GecsPacketCodec.BuildPacket("PG,01,PATTERN,INDEX,END,OK", 0x12);
        var buffer = new List<byte> { 0x00, 0x7F };
        buffer.AddRange(first.Take(5));

        Assert.False(GecsPacketCodec.TryReadFrame(buffer, out _));

        buffer.AddRange(first.Skip(5));
        buffer.AddRange(second);

        Assert.True(GecsPacketCodec.TryReadFrame(buffer, out GecsFrame alive));
        Assert.Equal(0xFF, alive.NetworkNumber);
        Assert.Equal("ALIVE", alive.MessageText);
        Assert.True(GecsPacketCodec.TryReadFrame(buffer, out GecsFrame response));
        Assert.Equal(0x12, response.NetworkNumber);
        Assert.Equal("PG,01,PATTERN,INDEX,END,OK", response.MessageText);
        Assert.Empty(buffer);
    }

    [Fact]
    public void BuildPacketRejectsNonAsciiMessageText()
    {
        Assert.Throws<ArgumentException>(() => GecsPacketCodec.BuildPacket("PG,01,中文", 0xFF));
    }

    [Fact]
    public void TryReadFrameRejectsInvalidHexLength()
    {
        var buffer = new List<byte> { 0x02, 0xFF, (byte)'0', (byte)'0', (byte)'G', (byte)'1', (byte)'A', 0x03 };

        Assert.Throws<InvalidDataException>(() => GecsPacketCodec.TryReadFrame(buffer, out _));
    }

    [Fact]
    public void TryReadFrameRejectsMissingEtxAndCanResynchronize()
    {
        byte[] invalid = GecsPacketCodec.BuildPacket("A", 0xFF);
        invalid[^1] = 0x04;
        var buffer = invalid.ToList();

        Assert.Throws<InvalidDataException>(() => GecsPacketCodec.TryReadFrame(buffer, out _));
        Assert.DoesNotContain(GecsPacketCodec.Stx, buffer.Take(1));
    }
}
