#pragma warning disable CA1707
using System.Reflection;

namespace ColorVision.UI.Tests;

public class MenuDiscoveryExclusionTests
{
    [Theory]
    [InlineData("ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.MenuThirdPartyAlgorithms")]
    [InlineData("ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.MenuItemProviderSensor")]
    public void ObsoleteMenuType_IsExcludedFromMenuDiscovery(string typeName)
    {
        Type menuType = typeof(ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.TemplateThirdParty).Assembly.GetType(typeName, throwOnError: true)!;
        Assert.NotNull(menuType.GetCustomAttribute<ObsoleteAttribute>(inherit: false));

        MethodInfo? candidateCheck = typeof(ColorVision.UI.Menus.MenuManager).GetMethod("IsConcreteMenuCandidate", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(candidateCheck);
        Assert.False(Assert.IsType<bool>(candidateCheck.Invoke(null, [menuType])));
    }
}
