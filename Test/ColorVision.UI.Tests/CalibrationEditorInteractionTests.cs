using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.PhyCameras.Calibration;
using ColorVision.Engine.Services.PhyCameras.Group;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public class CalibrationEditorInteractionTests
{
    [Fact]
    public void ChangingTemplateReferencePreservesEnableChoiceAndDoesNotInventResourceId()
    {
        var param = new CalibrationBase(new List<ModDetailModel>()) { FilePath = "old.dat", Id = 12, IsSelected = true };
        var row = new CalibrationControlRow(CalibrationSlotDefinitions.NormalSlots[0], param, Array.Empty<CalibrationResource>());
        row.FileReference = "replacement.dat";
        Assert.Equal("replacement.dat", param.FilePath);
        Assert.Equal(0, param.Id);
        Assert.True(param.IsSelected);
        Assert.Equal("本机缺失", row.FileStatus);
        row.FileReference = "";
        Assert.Equal("未配置", row.FileStatus);
        Assert.True(param.IsSelected);
    }

    [Fact]
    public void BinaryCalibrationTypesNeverUseTextEditor()
    {
        var binary = new[] { "DefectPoint", "DSNU", "Uniformity", "LineArity" };
        foreach (var slot in CalibrationSlotDefinitions.AllSlots)
            Assert.Equal(!binary.Contains(slot.Key), CalibrationResource.SupportsTextEditing(slot.ServiceType));
        Assert.False(CalibrationResource.SupportsTextEditing((ColorVision.Engine.Services.Types.ServiceTypes)(-1)));
    }

    [Fact]
    public void FourColorWorkflowWindowLoadsWithRuntimeThemeResources()
    {
        WpfTestHost.Invoke(() =>
        {
            List<ResourceDictionary> dictionaries = new();
            LumFourColorCalibrationWorkflowWindow? window = null;
            try
            {
                foreach (string source in new[]
                {
                    "/HandyControl;component/Themes/basic/colors/colors.xaml",
                    "/HandyControl;component/Themes/Theme.xaml",
                    "/ColorVision.Themes;component/Themes/White.xaml",
                    "/ColorVision.Themes;component/Themes/Base.xaml",
                })
                {
                    ResourceDictionary dictionary = new() { Source = new Uri(source, UriKind.Relative) };
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                    dictionaries.Add(dictionary);
                }

                window = new LumFourColorCalibrationWorkflowWindow(Array.Empty<DeviceCamera>(), Array.Empty<DeviceSpectrum>());
                Grid root = Assert.IsType<Grid>(window.Content);
                root.Measure(new Size(1204, 738));
                root.Arrange(new Rect(0, 0, 1204, 738));
                root.UpdateLayout();

                Assert.Equal("R", Assert.IsType<ListBox>(window.FindName("SampleList")).Items.Cast<LumFourColorCalibrationSample>().First().Name);
                Assert.NotNull(Assert.IsType<Button>(window.FindName("CaptureCameraButton")).Style);
                Assert.False(Assert.IsType<Button>(window.FindName("CalculateButton")).IsEnabled);
            }
            finally
            {
                window?.Close();
                foreach (ResourceDictionary dictionary in dictionaries)
                    Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            }
        });
    }
}
