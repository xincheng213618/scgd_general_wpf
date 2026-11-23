using ColorVision.SocketProtocol;
using ColorVision.ToolPlugins.ThirdPartyApps;
using Newtonsoft.Json;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class SscomArvrPresetsTests
{
    [Fact]
    public void BuildCreatesFourCompactManualRequestsWithTheEnteredSerialNumber()
    {
        const string sn = "测试\"SN\\001";
        var presets = SscomArvrPresets.Build(" " + sn + " ");
        string[] events = ["GetProcessEnable", "ProjectARVRInit", "RunAll", "MeasureVID"];
        Assert.Equal(events.Length, presets.Count);
        var messageIds = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < presets.Count; index++)
        {
            var preset = presets[index];
            Assert.Contains(events[index], preset.Name);
            Assert.Equal(0, preset.DelayMilliseconds);
            Assert.Equal((byte)'{', preset.Payload[0]);
            Assert.Equal((byte)'}', preset.Payload[^1]);
            string json = Encoding.UTF8.GetString(preset.Payload);
            Assert.DoesNotContain('\r', json);
            Assert.DoesNotContain('\n', json);

            var request = Assert.IsType<SocketRequest>(JsonConvert.DeserializeObject<SocketRequest>(json));
            Assert.Equal("1.0", request.Version);
            Assert.Equal(events[index], request.EventName);
            Assert.True(Guid.TryParseExact(request.MsgID, "N", out _));
            Assert.True(messageIds.Add(request.MsgID));
            Assert.Equal(index == 0 ? null : sn, request.SerialNumber);
            Assert.Null(request.Params);
        }
    }

    [Fact]
    public void BuildRequiresASerialNumberForTheMeasurementCommands()
    {
        Assert.Throws<ArgumentException>(() => SscomArvrPresets.Build("  "));
    }
}
