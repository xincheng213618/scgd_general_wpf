using FlowEngineLib.Base;
using FlowEngineLib.Node.OLED;

namespace ColorVision.UI.Tests;

public class LegacyServerHubInputTests
{
    [Fact]
    public void Algorithm2InReadsMasterResultsFromCapturedLocalInputs()
    {
        CVStartCFC imageInput = new("mixed-flow");
        imageInput.MasterValue(null!, 37, 100);
        CVStartCFC poiInput = new("mixed-flow");
        poiInput.MasterValue(null!, 41, 25);
        TestableAlgorithm2InNode node = new();

        Algorithm2InParam payload = node.BuildPayload(imageInput, poiInput);

        Assert.Equal(37, payload.MasterId);
        Assert.Equal(100, payload.MasterResultType);
        Assert.Equal(41, payload.POI_MasterId);
    }

    private sealed class TestableAlgorithm2InNode : Algorithm2InNode
    {
        public Algorithm2InParam BuildPayload(CVStartCFC imageInput, CVStartCFC poiInput)
        {
            masterInput[0] = new CVStartCFC(imageInput);
            masterInput[1] = new CVStartCFC(poiInput);
            return (Algorithm2InParam)getBaseEventData(imageInput);
        }
    }
}
