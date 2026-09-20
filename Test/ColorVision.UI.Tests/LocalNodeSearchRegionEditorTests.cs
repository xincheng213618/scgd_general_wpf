using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.UI;
using System.ComponentModel;
using System.Reflection;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class LocalNodeSearchRegionEditorTests
{
    [Theory]
    [InlineData(typeof(LocalRgbCrossNode))]
    [InlineData(typeof(LocalFindCrossNode))]
    [InlineData(typeof(LocalGridDistortionNode))]
    public void PoiSelectionHidesManualRegionWithoutDiscardingIt(Type type)
    {
        WpfTestHost.Invoke(() =>
        {
            var node = Activator.CreateInstance(type)!;
            var region = type.GetProperty("SearchRegion")!;
            var poi = type.GetProperty("SearchRegionPoiTemplate")!;
            var expected = new Int32Rect(10, 20, 200, 200); region.SetValue(node, expected);
            var editor = PropertyEditorHelper.GenProperties(region, node);
            Assert.Equal(Visibility.Visible, editor.Visibility);
            poi.SetValue(node, "POI_W255"); Assert.Equal(Visibility.Collapsed, editor.Visibility);
            Assert.Equal(expected, region.GetValue(node));
            poi.SetValue(node, " "); Assert.Equal(Visibility.Visible, editor.Visibility);
            Assert.Equal(expected, region.GetValue(node));
            Assert.Equal("搜索区域", poi.GetCustomAttribute<CategoryAttribute>()!.Category);
            Assert.Equal("搜索区域", region.GetCustomAttribute<CategoryAttribute>()!.Category);
        });
    }
    [Theory]
    [InlineData(typeof(LocalRgbCrossNode))]
    [InlineData(typeof(LocalFindCrossNode))]
    [InlineData(typeof(LocalGridDistortionNode))]
    [InlineData(typeof(LocalFileFusionNode))]
    public void ResultDirectoriesUseFolderPicker(Type type)
        => Assert.Equal(typeof(TextSelectFolderPropertiesEditor), type.GetProperty("ResultDirectory")!.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType);
}
