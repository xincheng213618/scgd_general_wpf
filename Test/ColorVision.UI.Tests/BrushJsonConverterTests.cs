#pragma warning disable CA1707
using ColorVision.UI.Json;
using Newtonsoft.Json;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public class BrushJsonConverterTests
{
    [Fact]
    public void BrushJsonConverter_WritesAndReadsSolidColorBrush()
    {
        var settings = new JsonSerializerSettings();
        settings.Converters.Add(new BrushJsonConverter());
        var holder = new BrushHolder
        {
            Brush = new SolidColorBrush(Color.FromRgb(0xD3, 0x2F, 0x2F))
        };

        var json = JsonConvert.SerializeObject(holder, settings);
        var roundTrip = JsonConvert.DeserializeObject<BrushHolder>(json, settings);

        Assert.Contains("#FFD32F2F", json);
        var brush = Assert.IsType<SolidColorBrush>(roundTrip?.Brush);
        Assert.Equal(Color.FromRgb(0xD3, 0x2F, 0x2F), brush.Color);
    }

    private sealed class BrushHolder
    {
        public Brush? Brush { get; set; }
    }
}
