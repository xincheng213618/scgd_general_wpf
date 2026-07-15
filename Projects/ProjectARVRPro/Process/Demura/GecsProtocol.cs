using System.Text;

namespace ProjectARVRPro.Process.Demura
{
    public sealed class GecsCommand
    {
        public string Name { get; }

        public string MessageText { get; }

        public IReadOnlyList<string> SuccessResponses { get; }

        public byte[] Packet => GecsProtocol.BuildPacket(MessageText);

        public string PacketText => $"[02][FF]{GecsProtocol.BuildLengthPrefixedMessage(MessageText)}[03]";

        public GecsCommand(string name, string messageText, params string[] successResponses)
        {
            Name = name;
            MessageText = messageText;
            SuccessResponses = successResponses;
        }
    }

    public static class GecsProtocol
    {
        public const string DefaultDemuraFileName = "DemuraDynamic.bin";
        public const string DemuraControlChannel = "1";

        public static GecsCommand SendFile(string channel, int fileIndex, string sourceFile, string targetFileName, string successResponse)
        {
            return new GecsCommand("SendFile", $"PG,{channel},SENDFILE,START,{fileIndex},{sourceFile},{targetFileName}", successResponse);
        }

        public static GecsCommand QueryPowerState()
        {
            return new GecsCommand("PowerState", $"PG,{DemuraControlChannel},POWER,STATE", "POWER,STATE,ON", "POWER,STATE,OFF");
        }

        public static GecsCommand PowerOn()
        {
            return new GecsCommand("PowerOn", $"PG,{DemuraControlChannel},POWER,ON", "POWER,ON,END,OK");
        }

        public static GecsCommand PowerOff()
        {
            return new GecsCommand("PowerOff", $"PG,{DemuraControlChannel},POWER,OFF", "POWER,OFF,END,OK");
        }

        public static GecsCommand DemuraErase()
        {
            return new GecsCommand("Erase", $"PG,{DemuraControlChannel},DEMURA,ERASE,START", "DEMURA,ERASE,END,OK");
        }

        public static GecsCommand DemuraWrite()
        {
            return new GecsCommand("Write", $"PG,{DemuraControlChannel},DEMURA,WRITE,START", "DEMURA,WRITE,END,OK");
        }

        public static IReadOnlyList<GecsCommand> DemuraBurnAfterPowerState(bool isPowerOn)
        {
            return isPowerOn
                ? new[] { DemuraErase(), DemuraWrite() }
                : new[] { PowerOn(), DemuraErase(), DemuraWrite() };
        }

        public static bool TryGetPowerState(string responseText, out bool isPowerOn)
        {
            if (responseText.Contains("POWER,STATE,ON", StringComparison.OrdinalIgnoreCase))
            {
                isPowerOn = true;
                return true;
            }

            if (responseText.Contains("POWER,STATE,OFF", StringComparison.OrdinalIgnoreCase))
            {
                isPowerOn = false;
                return true;
            }

            isPowerOn = false;
            return false;
        }

        public static string ResolveDemuraTargetFileName(string? configuredTargetFileName)
        {
            string targetFileName = configuredTargetFileName?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(targetFileName) || string.Equals(targetFileName, "DemuraMerged.bin", StringComparison.OrdinalIgnoreCase)
                ? DefaultDemuraFileName
                : targetFileName;
        }

        public static byte[] BuildPacket(string messageText)
        {
            byte[] framedMessage = Encoding.UTF8.GetBytes(BuildLengthPrefixedMessage(messageText));
            byte[] packet = new byte[framedMessage.Length + 3];
            packet[0] = 0x02;
            packet[1] = 0xFF;
            Buffer.BlockCopy(framedMessage, 0, packet, 2, framedMessage.Length);
            packet[^1] = 0x03;
            return packet;
        }

        public static string BuildLengthPrefixedMessage(string messageText)
        {
            return $"{messageText.Length:X4}{messageText}";
        }
    }
}
