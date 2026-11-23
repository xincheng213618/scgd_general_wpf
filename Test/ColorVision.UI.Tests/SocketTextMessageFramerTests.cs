using System.Text;
using ColorVision.SocketProtocol;

namespace ColorVision.UI.Tests;

public sealed class SocketTextMessageFramerTests
{
    [Fact]
    public void MixedReadsPreserveJsonStringsAndUtf8AcrossPacketBoundaries()
    {
        const string handshake = "T0000,SN001;";
        const string set = """{"method":"set","ResultSavePath":"C:\\结果;{A}"}""";
        const string get = """{"method":"get","param":["ResultSavePath"]}""";
        byte[] first = Encoding.UTF8.GetBytes(handshake + "\r\n" + set);
        int split = Encoding.UTF8.GetByteCount((handshake + "\r\n" + set).Split('结')[0]) + 1;
        byte[] second = first[split..].Concat(Encoding.UTF8.GetBytes(get + handshake)).ToArray();

        using var framer = new SocketTextMessageFramer();
        Assert.Equal([handshake], framer.Read(first, split));
        Assert.Equal([set, get, handshake], framer.Read(second, second.Length));

        byte[] undelimited = Encoding.UTF8.GetBytes("T0000,SN002");
        Assert.Equal(["T0000,SN002"], framer.Read(undelimited, undelimited.Length));
    }
}
