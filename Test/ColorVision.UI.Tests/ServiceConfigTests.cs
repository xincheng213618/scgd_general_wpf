using ColorVision.Engine.Services.RC;

namespace ColorVision.UI.Tests;

public class ServiceConfigTests
{
    [Fact]
    public void ReplacingRegistrationCenterServiceInfoRaisesPropertyChanged()
    {
        ServiceConfig config = new();
        string? changedProperty = null;
        config.PropertyChanged += (_, e) => changedProperty = e.PropertyName;

        config.RegistrationCenterServiceInfo = new ServiceInfo { FileVersion = "3.4.1.814" };

        Assert.Equal(nameof(ServiceConfig.RegistrationCenterServiceInfo), changedProperty);
    }
}
