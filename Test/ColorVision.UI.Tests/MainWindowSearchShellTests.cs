using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

/// <summary>Shell markup contracts; never starts MainWindow, device discovery or production configuration.</summary>
public sealed class MainWindowSearchShellTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Search = "clr-namespace:ColorVision.UI.Serach;assembly=ColorVision.UI";

    [Fact]
    public void MainWindowDoesNotEmbedSearchOrATopBarSearchEntry()
    {
        XDocument document = LoadShell();
        Assert.Empty(document.Descendants(Search + "SearchControl"));
        Assert.Empty(document.Descendants(Presentation + "Popup"));
        Assert.DoesNotContain(document.Descendants(), element => element.Attribute(Xaml + "Name")?.Value is "SearchEntryButton" or "SearchEntryGestureText");
        Assert.DoesNotContain(document.Descendants().Attributes(), attribute => attribute.Value.Contains("SearchEntryPlaceholder", StringComparison.Ordinal));
        Assert.Single(document.Descendants(Presentation + "Window"));
    }

    [Fact]
    public void SearchWindowHasAStandardResizableOwnerCenteredShell()
    {
        XDocument document = LoadSearchWindow();
        XElement window = Assert.IsType<XElement>(document.Root);
        Assert.Equal(Presentation + "Window", window.Name);
        Assert.Equal("ColorVision.UI.Serach.SearchWindow", window.Attribute(Xaml + "Class")?.Value);
        Assert.Equal("CanResize", window.Attribute("ResizeMode")?.Value);
        Assert.Equal("SingleBorderWindow", window.Attribute("WindowStyle")?.Value);
        Assert.Equal("CenterOwner", window.Attribute("WindowStartupLocation")?.Value);
        Assert.Equal("False", window.Attribute("ShowInTaskbar")?.Value);
        Assert.NotEqual("True", window.Attribute("AllowsTransparency")?.Value);
        Assert.NotEqual("True", window.Attribute("Topmost")?.Value);
        Assert.Empty(window.Descendants(Presentation + "Window"));
        Assert.Empty(window.Descendants(Presentation + "Popup"));
        XElement control = Assert.Single(window.Descendants(), element => element.Name.LocalName == "SearchControl");
        Assert.Equal("CommandSearchControl", control.Attribute(Xaml + "Name")?.Value);
    }

    [Fact]
    public void SearchRemainsAvailableThroughConfigurableMenuActions()
    {
        var search = new MenuCommandSearch();
        var find = new MenuContextualFind();
        Assert.IsAssignableFrom<IHotKey>(search);
        Assert.IsAssignableFrom<IHotKey>(find);
        Assert.Equal(MenuItemConstants.Tool, search.OwnerGuid);
        Assert.Equal(MenuItemConstants.Edit, find.OwnerGuid);
        Assert.Equal(new Hotkey(Key.P, ModifierKeys.Control | ModifierKeys.Shift), search.HotKeys.Hotkey);
        Assert.Equal(new Hotkey(Key.F, ModifierKeys.Control), find.HotKeys.Hotkey);
    }

    private static XDocument LoadShell([CallerFilePath] string testPath = "")
        => XDocument.Load(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testPath)!, "..", "..", "ColorVision", "MainWindow.xaml")));

    private static XDocument LoadSearchWindow([CallerFilePath] string testPath = "")
        => XDocument.Load(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testPath)!, "..", "..", "UI", "ColorVision.UI", "Serach", "SearchWindow.xaml")));
}
