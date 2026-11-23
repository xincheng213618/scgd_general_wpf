using ColorVision.ToolPlugins.ThirdPartyApps;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ColorVision.UI.Tests;

public sealed class SscomPresetIniTests
{
    private static readonly Encoding Cp936 = CodePagesEncodingProvider.Instance.GetEncoding(936,
        EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)!;

    [Fact]
    public void BuildWritesNinetyNinePairsInCp936AndKeepsTerminatorsInsideHexPayload()
    {
        byte[] result = SscomPresetIni.Build([new("读取,光谱", [0x52, 0x00, 0x0d, 0x0a, 0xff], 100)]);
        string ini = Cp936.GetString(result);

        Assert.False(result.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.DoesNotContain('\n', ini.Replace("\r\n", ""));
        Assert.DoesNotContain('\r', ini.Replace("\r\n", ""));
        Assert.Contains("N101=0,读取，光谱,100\r\nN1=H,52 00 0D 0A FF\r\n", ini);
        Assert.Contains("N199=0,,1000\r\nN99=A,\r\n", ini);
        Assert.Equal(99, Regex.Count(ini, @"(?m)^N(?:[1-9]|[1-9][0-9])=[HA],"));
        Assert.Equal(99, Regex.Count(ini, @"(?m)^N1(?:0[1-9]|[1-9][0-9])=0,"));
    }

    [Fact]
    public void TcpClientProfileKeepsManualSendingAndUsesTheSpecifiedEndpoint()
    {
        string ini = Cp936.GetString(SscomPresetIni.Build([new("状态\r\nN1063=,Y", [0x31])], new("127.0.0.1", 6666)));

        Assert.Contains("\r\nN1068=,127.0.0.1\r\nN1069=,6666\r\n", ini);
        Assert.Contains("\r\nN1080=,1\r\n", ini);
        Assert.Contains("\r\nN1063=,N\r\n", ini);
        Assert.Contains("\r\nN1060=,0\r\n", ini);
        Assert.Contains("\r\nN1057=,N\r\n", ini);
        Assert.Contains("\r\nN1071=,N\r\nN1072=,N\r\n", ini);
        Assert.Single(Regex.Matches(ini, @"(?m)^N1063="));
    }

    [Fact]
    public void BuildValidatesTheCommandCountAndPlacesTheFinalCommandInSlotNinetyNine()
    {
        SscomCommandPreset[] complete = Enumerable.Range(1, 99).Select(slot => new SscomCommandPreset($"指令 {slot}", [1])).ToArray();

        Assert.Contains("N199=0,指令 99,0\r\nN99=H,01\r\n", Cp936.GetString(SscomPresetIni.Build(complete)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SscomPresetIni.Build([]));
        Assert.Throws<ArgumentOutOfRangeException>(() => SscomPresetIni.Build([.. complete, complete[0]]));
        Assert.Throws<ArgumentException>(() => SscomPresetIni.Build([new("空指令", [])]));
        Assert.Throws<ArgumentException>(() => SscomPresetIni.Build([new("延时无效", [1], -1)]));
    }

    [Theory]
    [InlineData("127.0.0.1", 0)]
    [InlineData("127.0.0.1", 65536)]
    [InlineData("127.0.0.1\r\nN1063=,Y", 6666)]
    [InlineData("127.000.0.1", 6666)]
    public void BuildRejectsInvalidTcpEndpoint(string host, int port)
    {
        Assert.ThrowsAny<ArgumentException>(() => SscomPresetIni.Build([new("状态", [1])], new(host, port)));
    }

    [Fact]
    public void WritingNewPresetDoesNotReplaceAnExistingIni()
    {
        string directory = Directory.CreateTempSubdirectory("ColorVision-SscomIni-").FullName;
        string existing = Path.Combine(directory, "sscom51.ini");
        string exported = Path.Combine(directory, "ColorVision 预设.ini");
        SscomCommandPreset[] commands = [new("状态", [1])];
        try
        {
            File.WriteAllText(existing, "original settings");
            Assert.ThrowsAny<IOException>(() => SscomPresetIni.WriteNew(existing, commands));
            Assert.Equal("original settings", File.ReadAllText(existing));
            SscomPresetIni.WriteNew(exported, commands);
            Assert.Equal(SscomPresetIni.Build(commands), File.ReadAllBytes(exported));
            Assert.Equal(2, Directory.GetFiles(directory).Length);
        }
        finally
        {
            File.Delete(existing);
            File.Delete(exported);
            Directory.Delete(directory);
        }
    }
}
