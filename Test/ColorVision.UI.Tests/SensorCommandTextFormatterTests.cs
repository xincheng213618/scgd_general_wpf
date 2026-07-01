using ColorVision.Engine.Services.Devices.Sensor.Templates;

namespace ColorVision.UI.Tests;

public class SensorCommandTextFormatterTests
{
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
