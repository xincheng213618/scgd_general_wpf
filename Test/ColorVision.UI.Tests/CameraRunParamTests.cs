using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;

namespace ColorVision.UI.Tests;

public class CameraRunParamTests
{
    [Fact]
    public void SetAllExposure_UpdatesEveryExposureField()
    {
        var param = new CameraRunParam
        {
            ExpTime = 1,
            ExpTimeR = 2,
            ExpTimeG = 3,
            ExpTimeB = 4
        };

        param.SetAllExposure(80);

        Assert.Equal(80f, param.ExpTime);
        Assert.Equal(80f, param.ExpTimeR);
        Assert.Equal(80f, param.ExpTimeG);
        Assert.Equal(80f, param.ExpTimeB);
    }

}
