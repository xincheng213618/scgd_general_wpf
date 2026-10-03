using ColorVision.Engine.Services.Devices.Sensor.Templates;
using ColorVision.Engine;
using MQTTMessageLib.Sensor;

namespace ColorVision.UI.Tests;

public class SensorCommandTextFormatterTests
{
    [Theory]
    [InlineData(SensorCmdType.Ascii, "AT\r\n", "41 54 0D 0A")]
    [InlineData(SensorCmdType.UTF8, "中文", "E4 B8 AD E6 96 87")]
    [InlineData(SensorCmdType.GBK, "中文", "D6 D0 CE C4")]
    [InlineData(SensorCmdType.UTF7, "A+", "41 2B 2D")]
    public void ConversionPreservesServiceBytes(SensorCmdType type, string text, string hex)
    {
        Assert.Equal(hex, SensorCommandTextFormatter.ConvertEncoding(text, type, SensorCmdType.Hex));
        Assert.Equal(text, SensorCommandTextFormatter.ConvertEncoding(hex, SensorCmdType.Hex, type));
    }

    [Fact]
    public void ExplicitConversionUpdatesBothFieldsAndPreservesTimingAndValueB()
    {
        const string stored = "AT\r\n,OK,Ascii,1500/20,3";
        var model = new ModDetailModel { ValueA = stored, ValueB = "backup" };
        var command = new SensorCommand(model);
        Assert.Equal(stored, model.ValueA);
        Assert.True(command.TryConvertEncoding(SensorCmdType.Hex));
        Assert.Equal("41 54 0D 0A,4F 4B,Hex,1500/20,3", model.ValueA);
        Assert.True(command.TryConvertEncoding(SensorCmdType.Ascii));
        Assert.Equal(stored, model.ValueA);
        Assert.Equal("backup", model.ValueB);
    }

    [Theory]
    [InlineData("41", "FF", SensorCmdType.Ascii)]
    [InlineData("41", "E4", SensorCmdType.UTF8)]
    [InlineData("41", "0", SensorCmdType.Ascii)]
    [InlineData("41 2C 42", "43", SensorCmdType.Ascii)]
    [InlineData("41", "42 2C 43", SensorCmdType.Ascii)]
    [InlineData("5C 72", "41", SensorCmdType.Ascii)]
    public void InvalidOrUnrepresentableConversionPreservesOriginalCommand(string request, string response, SensorCmdType target)
    {
        string stored = $"{request},{response},Hex,1000/0,0";
        var model = new ModDetailModel { ValueA = stored, ValueB = "backup" };
        var command = new SensorCommand(model);
        Assert.False(command.TryConvertEncoding(target));
        Assert.Equal(request, command.Request);
        Assert.Equal(response, command.Response);
        Assert.Equal(SensorCmdType.Hex, command.SensorCmdType);
        Assert.Equal(stored, model.ValueA);
        Assert.Equal("backup", model.ValueB);
    }

    [Fact]
    public void BracketConversionAlsoConvertsTheOtherFieldToKeepItsBytes()
    {
        var command = new SensorCommand(new ModDetailModel { ValueA = "Q,OK,Ascii,1000/0,0" }) { BracketText = "[STX]Q[ETX]" };
        command.BracketTextToRequestCommand.Execute(null);
        Assert.Equal("02 51 03", command.Request);
        Assert.Equal("4F 4B", command.Response);
        Assert.Equal(SensorCmdType.Hex, command.SensorCmdType);
    }

    [Fact]
    public void BracketTextToHexConvertsControlBytesAndAscii()
    {
        string hex = SensorCommandTextFormatter.BracketTextToHex("[02][FF]000EPG,1,POWER,OFF[03]");

        Assert.Equal("02 FF 30 30 30 45 50 47 2C 31 2C 50 4F 57 45 52 2C 4F 46 46 03", hex);
    }

    [Fact]
    public void BracketTextToHexAcceptsNamedControlBytes()
    {
        string hex = SensorCommandTextFormatter.BracketTextToHex("[STX][FF]000EPG,1,POWER,OFF[ETX]");

        Assert.Equal("02 FF 30 30 30 45 50 47 2C 31 2C 50 4F 57 45 52 2C 4F 46 46 03", hex);
    }

    [Fact]
    public void TryHexToBracketTextConvertsSampleBackToReadableText()
    {
        bool success = SensorCommandTextFormatter.TryHexToBracketText("02 FF 30 30 30 45 50 47 2C 31 2C 50 4F 57 45 52 2C 4F 46 46 03", out string bracketText);

        Assert.True(success);
        Assert.Equal("[02][FF]000EPG,1,POWER,OFF[03]", bracketText);
    }

    [Fact]
    public void TryHexToBracketTextCanUseControlNames()
    {
        bool success = SensorCommandTextFormatter.TryHexToBracketText("02 FF 30 30 30 45 50 47 2C 31 2C 50 4F 57 45 52 2C 4F 46 46 03", useControlNames: true, out string bracketText);

        Assert.True(success);
        Assert.Equal("[STX][FF]000EPG,1,POWER,OFF[ETX]", bracketText);
    }

    [Fact]
    public void NormalizeHexAcceptsCompactHex()
    {
        string hex = SensorCommandTextFormatter.NormalizeHex("02FF3030");

        Assert.Equal("02 FF 30 30", hex);
    }
}
