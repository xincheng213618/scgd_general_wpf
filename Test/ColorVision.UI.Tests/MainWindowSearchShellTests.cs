using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;

namespace ColorVision.UI.Tests;

/// <summary>Shell markup contracts; never starts MainWindow, device discovery or production configuration.</summary>
public sealed class MainWindowSearchShellTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Search = "clr-namespace:ColorVision.UI.Serach;assembly=ColorVision.UI";

    [Fact]
    public void SearchIsOneAttachedPopupWithoutAnIndependentApplicationWindow()
    {
        XDocument document = LoadShell();
        XElement control = Assert.Single(document.Descendants(Search + "SearchControl"));
        Assert.Equal("CommandSearchControl", control.Attribute(Xaml + "Name")?.Value);
        XElement popup = Assert.Single(document.Descendants(Presentation + "Popup"));
        Assert.Same(popup, control.Parent);
        Assert.Equal("CommandSearchPopup", popup.Attribute(Xaml + "Name")?.Value);
        Assert.Equal("{Binding ElementName=Root}", popup.Attribute("PlacementTarget")?.Value);
        Assert.Equal("True", popup.Attribute("AllowsTransparency")?.Value);
        Assert.Equal("False", popup.Attribute("StaysOpen")?.Value);
        Assert.Equal("False", popup.Attribute("Focusable")?.Value);
        Assert.Equal("Root", popup.Parent?.Attribute(Xaml + "Name")?.Value);
        Assert.Single(document.Descendants(Presentation + "Window"));
    }

    [Fact]
    public void SearchOverlayIsCenteredResponsiveAndWiredForDismissal()
    {
        XElement control = Assert.Single(LoadShell().Descendants(Search + "SearchControl"));
        Assert.Equal("720", control.Attribute("MaxWidth")?.Value);
        Assert.Equal("Stretch", control.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Top", control.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("CommandSearchControl_Closed", control.Attribute("Closed")?.Value);
        Assert.Equal("Custom", control.Parent?.Attribute("Placement")?.Value);
        Assert.Equal("CommandSearchPopup_Closed", control.Parent?.Attribute("Closed")?.Value);
        Assert.Equal("CommandSearchPopup_Opened", control.Parent?.Attribute("Opened")?.Value);
    }

    [Theory]
    [InlineData(1180, 720, 230)]
    [InlineData(630, 598, 16)]
    [InlineData(1280, 720, 280)]
    public void PopupPlacementStaysCenteredNearTheTopOfItsOwner(double hostWidth, double paletteWidth, double expectedX)
    {
        CustomPopupPlacement placement = Assert.Single(MainWindow.PlaceCommandSearch(new Size(paletteWidth, 400), new Size(hostWidth, 700), new Point()));
        Assert.Equal(new Point(expectedX, 48), placement.Point);
        Assert.Equal(PopupPrimaryAxis.None, placement.PrimaryAxis);
    }

    [Fact]
    public void SearchEntrySupportsMouseAndKeyboardWithoutHardcodedGestureText()
    {
        XDocument document = LoadShell();
        XElement entry = Assert.Single(document.Descendants(Presentation + "Button"), element => element.Attribute(Xaml + "Name")?.Value == "SearchEntryButton");
        Assert.Equal("SearchEntryButton_Click", entry.Attribute("Click")?.Value);
        Assert.Equal("SearchEntryButton_PreviewMouseLeftButtonDown", entry.Attribute("PreviewMouseLeftButtonDown")?.Value);
        Assert.NotNull(entry.Attribute("AutomationProperties.Name"));
        Assert.NotEqual("False", entry.Attribute("Focusable")?.Value);
        XElement gesture = Assert.Single(entry.Descendants(Presentation + "TextBlock"), element => element.Attribute(Xaml + "Name")?.Value == "SearchEntryGestureText");
        Assert.Null(gesture.Attribute("Text"));
    }

    private static XDocument LoadShell([CallerFilePath] string testPath = "")
        => XDocument.Load(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testPath)!, "..", "..", "ColorVision", "MainWindow.xaml")));
}
