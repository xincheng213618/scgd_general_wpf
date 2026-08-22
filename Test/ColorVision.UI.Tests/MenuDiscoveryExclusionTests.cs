#pragma warning disable CA1707
using System.Reflection;

namespace ColorVision.UI.Tests;

public class MenuDiscoveryExclusionTests
{
    [Theory]
    [InlineData("ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.MenuThirdPartyAlgorithms")]
    [InlineData("ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.MenuItemProviderSensor")]
    [InlineData("ColorVision.Engine.Templates.Menus.MenuITemplateAlgorithm")]
    [InlineData("ColorVision.Engine.Templates.FocusPoints.ExportFocusPoints")]
    [InlineData("ColorVision.Engine.Templates.FindLightArea.ExportRoi")]
    [InlineData("ColorVision.Engine.Templates.Matching.ExportMenuItemMatching")]
    [InlineData("ColorVision.Engine.Templates.Jsons.ImageROI.ExportTemplateImageROI")]
    [InlineData("ColorVision.Engine.Templates.Jsons.OLEDImageProcessing.MenuLocalizationImageEnhancement")]
    [InlineData("ColorVision.Engine.Templates.Jsons.OLEDImageProcessing.MenuDediffusion")]
    [InlineData("ColorVision.Engine.Templates.Jsons.LEDStripDetectionV2.MenuLEDStripDetectionV2")]
    [InlineData("ColorVision.Engine.Templates.Jsons.Ghost2.MenuGhost2")]
    [InlineData("ColorVision.Engine.Templates.Jsons.OLEDAOI.MenuOLEDAOI")]
    [InlineData("ColorVision.Engine.Templates.Jsons.DetectScreenDefects.MenuDetectScreenDefects")]
    [InlineData("ColorVision.Engine.Services.Devices.Camera.Templates.MenuItemCamera")]
    [InlineData("ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam.MenuAutoExpTime")]
    [InlineData("ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus.MenuAutoFocus")]
    [InlineData("ColorVision.Engine.Services.Devices.Camera.Templates.HDR.MenuHDR")]
    [InlineData("ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam.MenuICameraExp")]
    [InlineData("ColorVision.Engine.Templates.Jsons.AutoExpTime.MenuAutoExpTimeV2")]
    [InlineData("ColorVision.Engine.Services.Devices.PG.Templates.ExportPGParam")]
    [InlineData("ColorVision.Database.ExportMySqlMenuItem")]
    [InlineData("ColorVision.Database.ExportMySqlConnect")]
    [InlineData("ColorVision.Database.ExportMySqlInitTables")]
    [InlineData("ColorVision.Database.ExportDatabaseCleanupTool")]
    [InlineData("ColorVision.Engine.MQTT.ExportMQTTMenuItem")]
    [InlineData("ColorVision.Engine.MQTT.ExportMQTTConnect")]
    [InlineData("ColorVision.Solution.Workspace.MenuSaveLayout")]
    [InlineData("ColorVision.Solution.Workspace.MenuApplyLayout")]
    [InlineData("ColorVision.ExportMenuViewMax")]
    public void ObsoleteMenuType_IsExcludedFromMenuDiscovery(string typeName)
    {
        Assembly[] menuAssemblies =
        [
            typeof(ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.TemplateThirdParty).Assembly,
            typeof(ColorVision.Database.MySqlControl).Assembly,
            typeof(ColorVision.Solution.Workspace.WorkspaceManager).Assembly,
            typeof(ColorVision.App).Assembly,
        ];
        Type menuType = menuAssemblies
            .Select(assembly => assembly.GetType(typeName, throwOnError: false))
            .FirstOrDefault(type => type != null)
            ?? throw new InvalidOperationException($"Menu type not found: {typeName}");
        Assert.NotNull(menuType.GetCustomAttribute<ObsoleteAttribute>(inherit: false));

        MethodInfo? candidateCheck = typeof(ColorVision.UI.Menus.MenuManager).GetMethod("IsConcreteMenuCandidate", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(candidateCheck);
        Assert.False(Assert.IsType<bool>(candidateCheck.Invoke(null, [menuType])));
    }

    [Fact]
    public void RemainingMenuEntries_AreDiscoverableAtExpectedLocations()
    {
        MethodInfo? candidateCheck = typeof(ColorVision.UI.Menus.MenuManager).GetMethod("IsConcreteMenuCandidate", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(candidateCheck);

        Assert.True(Assert.IsType<bool>(candidateCheck.Invoke(null, [typeof(ColorVision.Database.ExportMySqlTool)])));
        Assert.True(Assert.IsType<bool>(candidateCheck.Invoke(null, [typeof(ColorVision.Solution.Workspace.MenuResetLayout)])));

        var mySqlTool = new ColorVision.Database.ExportMySqlTool();
        Assert.Equal(ColorVision.UI.Menus.MenuItemConstants.View, mySqlTool.OwnerGuid);
        Assert.Equal(20, mySqlTool.Order);
    }
}
