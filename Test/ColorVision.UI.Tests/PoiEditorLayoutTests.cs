using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;

namespace ColorVision.UI.Tests;

public sealed class PoiEditorLayoutTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void MarginSettings_IsBelowCornersAlongsideSetAreaAndRetainsPopupAnchor()
    {
        XDocument document = LoadEditor();
        XElement button = document.Descendants(Presentation + "Button").Single(element => (string?)element.Attribute(Xaml + "Name") == "ButtonImportMarin");
        XElement actions = Assert.IsType<XElement>(button.Parent);
        Assert.Equal(Presentation + "WrapPanel", actions.Name);
        Assert.Contains(actions.Elements(Presentation + "Button"), element => (string?)element.Attribute("Click") == "ShowPoiConfig_Click");
        Assert.Equal(Presentation + "StackPanel", actions.Parent!.Name);
        Assert.Contains(actions.ElementsBeforeSelf().Descendants(Presentation + "TextBox"), element => ((string?)element.Attribute("Text"))?.Contains("PoiConfig.Polygon4Y", StringComparison.Ordinal) == true);
        XElement popup = actions.Element(Presentation + "Popup")!;
        Assert.Equal("{Binding ElementName=ButtonImportMarin}", (string?)popup.Attribute("PlacementTarget"));
    }

    [Fact]
    public void AutoFit_IsAQuadrilateralOnlyActionAndManualSizeFieldsRemainEditable()
    {
        XDocument document = LoadEditor();
        XElement button = document.Descendants(Presentation + "Button").Single(element => (string?)element.Attribute(Xaml + "Name") == "ButtonAutoFitPointSize");
        Assert.Contains("PoiConfig.IsAreaMask", (string?)button.Attribute("Visibility"));
        Assert.Equal("AutoFitPointSize_Click", (string?)button.Attribute("Click"));
        Assert.Equal(Presentation + "WrapPanel", button.Parent!.Name);
        foreach (string property in new[] { "DefaultCircleRadius", "DefaultRectWidth", "DefaultRectHeight" })
        {
            XElement field = document.Descendants(Presentation + "TextBox").Single(element => (string?)element.Attribute("Text") == $"{{Binding PoiConfig.{property}}}");
            Assert.Null(field.Attribute("IsReadOnly"));
            Assert.Null(field.Attribute("IsEnabled"));
        }
    }

    [Theory]
    [InlineData(260)]
    [InlineData(180)]
    public void MarginActions_MeasureWithoutOverlappingAtSidebarWidths(double width)
    {
        WpfTestHost.Invoke(() =>
        {
            XElement margin = LoadEditor().Descendants(Presentation + "Button").Single(element => (string?)element.Attribute(Xaml + "Name") == "ButtonImportMarin");
            XElement row = new(margin.Parent!);
            row.Elements(Presentation + "Popup").Remove();
            foreach (XElement button in row.Elements(Presentation + "Button"))
            {
                button.Attribute("Click")?.Remove();
                button.Attribute(Xaml + "Name")?.Remove();
                string resourceKey = button.Attribute("Content")!.Value.Replace("{x:Static properties:Resources.", "", StringComparison.Ordinal).TrimEnd('}');
                button.SetAttributeValue("Content", ColorVision.Engine.Properties.Resources.ResourceManager.GetString(resourceKey));
            }
            WrapPanel panel = Assert.IsType<WrapPanel>(XamlReader.Parse(row.ToString()));
            panel.Measure(new Size(width, double.PositiveInfinity));
            panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
            panel.UpdateLayout();
            Button[] buttons = panel.Children.OfType<Button>().ToArray();
            Assert.Equal(2, buttons.Length);
            Rect first = new(buttons[0].TranslatePoint(new Point(), panel), buttons[0].RenderSize);
            Rect second = new(buttons[1].TranslatePoint(new Point(), panel), buttons[1].RenderSize);
            Assert.False(first.IntersectsWith(second));
            Assert.True(first.Right <= width && second.Right <= width);
        });
    }

    private static XDocument LoadEditor([CallerFilePath] string testPath = "")
        => XDocument.Load(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testPath)!,
            "..", "..", "Engine", "ColorVision.Engine", "Templates", "POI", "EditPoiParam.xaml")));
}
