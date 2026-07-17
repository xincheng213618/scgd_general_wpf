using ProjectARVRPro.Process.Demura;
using System.Text;
using Xunit;

namespace ProjectARVRPro.Tests
{
    public class GecsProtocolTests
    {
        [Fact]
        public void DefaultBurnTargetUsesDynamicBinAndMigratesLegacyDefault()
        {
            Assert.Equal("DemuraDynamic.bin", new DemuraProcessConfig().BurnTargetFileName);
            Assert.Equal("DemuraDynamic.bin", GecsProtocol.ResolveDemuraTargetFileName("DemuraMerged.bin"));
            Assert.Equal("Custom.bin", GecsProtocol.ResolveDemuraTargetFileName("Custom.bin"));
        }

        [Fact]
        public void PowerStateCommandMatchesGecsProtocol()
        {
            GecsCommand command = GecsProtocol.QueryPowerState();

            Assert.Equal("PG,1,POWER,STATE", command.MessageText);
            Assert.Equal("[02][FF]0010PG,1,POWER,STATE[03]", command.PacketText);
            Assert.Collection(command.SuccessResponses,
                response => Assert.Equal("POWER,STATE,ON", response),
                response => Assert.Equal("POWER,STATE,OFF", response));
        }

        [Theory]
        [InlineData("PG,1,POWER,STATE,ON", true)]
        [InlineData("PG,1,POWER,STATE,OFF", false)]
        public void PowerStateResponseIsParsed(string responseText, bool expectedPowerOn)
        {
            Assert.True(GecsProtocol.TryGetPowerState(responseText, out bool isPowerOn));
            Assert.Equal(expectedPowerOn, isPowerOn);
        }

        [Fact]
        public void DemuraEraseAndWriteCommandsMatchGecsProtocol()
        {
            GecsCommand erase = GecsProtocol.DemuraErase();
            GecsCommand write = GecsProtocol.DemuraWrite();

            Assert.Equal("[02][FF]0017PG,1,DEMURA,ERASE,START[03]", erase.PacketText);
            Assert.Equal("DEMURA,ERASE,END,OK", Assert.Single(erase.SuccessResponses));
            Assert.Equal("[02][FF]0017PG,1,DEMURA,WRITE,START[03]", write.PacketText);
            Assert.Equal("DEMURA,WRITE,END,OK", Assert.Single(write.SuccessResponses));
        }

        [Fact]
        public void DemuraBurnSkipsPowerOnWhenStateIsAlreadyOn()
        {
            IReadOnlyList<GecsCommand> commands = GecsProtocol.DemuraBurnAfterPowerState(true);

            Assert.Collection(commands,
                command => Assert.Equal("Erase", command.Name),
                command => Assert.Equal("Write", command.Name));
        }

        [Fact]
        public void DemuraBurnPowersOnBeforeEraseWhenStateIsOff()
        {
            IReadOnlyList<GecsCommand> commands = GecsProtocol.DemuraBurnAfterPowerState(false);

            Assert.Collection(commands,
                command => Assert.Equal("PowerOn", command.Name),
                command => Assert.Equal("Erase", command.Name),
                command => Assert.Equal("Write", command.Name));
        }

        [Fact]
        public void PacketUsesUtf8AndCharacterCountFromGecsExample()
        {
            const string message = "PG,01,SENDFILE,START,1,C:\\测试.bin,DemuraDynamic.bin";
            byte[] packet = GecsProtocol.BuildPacket(message);

            Assert.Equal(0x02, packet[0]);
            Assert.Equal(0xFF, packet[1]);
            Assert.Equal(0x03, packet[^1]);
            Assert.Equal($"{message.Length:X4}{message}", Encoding.UTF8.GetString(packet[2..^1]));
        }
    }
}
