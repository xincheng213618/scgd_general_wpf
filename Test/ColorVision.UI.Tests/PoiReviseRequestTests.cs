using FlowEngineLib.Node.POI;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests;

public sealed class PoiReviseRequestTests
{
    // Old services dereference OutputParam.Name before deciding whether to save a template.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("saved-revise-template")]
    public void RequestIncludesOutputDescriptorWhenTemplateSavingIsDisabled(string? outputName)
    {
        var request = new POIReviseData(30, 40, outputName!, "reference-point", true);

        JObject payload = JObject.FromObject(request);

        var output = Assert.IsType<JObject>(payload[nameof(request.OutputParam)]);
        Assert.Equal(-1, (int)output["ID"]!);
        Assert.Equal(outputName ?? string.Empty, (string?)output["Name"]);
    }
}
