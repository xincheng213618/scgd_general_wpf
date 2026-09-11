using ColorVision.Guidance;
using ColorVision.UI.Json;
using ColorVision.UI.Menus;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace ColorVision.UI.Tests;

public sealed class NewUserGuideTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void AutomaticGuideUsesOneBooleanState()
    {
        var config = new MainWindowConfig();

        Assert.False(config.HasShownNewUserGuide);
        Assert.True(MainWindow.TryRecordNewUserGuideOffer(config));
        Assert.True(config.HasShownNewUserGuide);
        Assert.False(MainWindow.TryRecordNewUserGuideOffer(config));

        var handler = new ConfigHandler();
        JObject saved = JObject.FromObject(config, JsonSerializer.Create(handler.JsonSerializerSettings));
        Assert.True(saved.Value<bool>(nameof(MainWindowConfig.HasShownNewUserGuide)));
    }

    [Fact]
    public void HelpMenuCanAlwaysRestartTheGuide()
    {
        var item = new MenuNewUserGuide();

        Assert.Equal(MenuItemConstants.Help, item.OwnerGuid);
        Assert.Equal(100, item.Order);
        Assert.False(string.IsNullOrWhiteSpace(item.Header));
    }

    [Fact]
    public void MainWindowHostsOneNonPopupGuideAboveAllThreeShellRows()
    {
        XDocument shell = LoadMainWindow();
        XElement guide = Assert.Single(shell.Descendants(), element =>
            element.Name.LocalName == nameof(NewUserGuideOverlay));

        Assert.Equal("NewUserGuideOverlay", guide.Attribute(Xaml + "Name")?.Value);
        Assert.Equal("3", guide.Attribute("Grid.RowSpan")?.Value);
        Assert.Equal("1000", guide.Attribute("Panel.ZIndex")?.Value);

        XDocument overlay = LoadGuideOverlay();
        Assert.Empty(overlay.Descendants(Presentation + "Popup"));
        foreach (string name in new[] { "FullShade", "SpotlightBorder", "WelcomeCard", "TourCard", "CompletionCard", "StartTourButton", "PreviousButton", "NextButton", "SkipButton" })
        {
            Assert.Single(overlay.Descendants(), element => element.Attribute(Xaml + "Name")?.Value == name);
        }
    }

    [Fact]
    public void UserFacingCopyDescribesTheActualLeftPanelSwitchAndUsesCompactCards()
    {
        XDocument resources = XDocument.Load(Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(GetTestPath())!, "..", "..", "ColorVision", "Guidance", "NewUserGuideResources.resx")));
        Dictionary<string, string> values = resources.Root!
            .Elements("data")
            .ToDictionary(element => element.Attribute("name")!.Value, element => element.Element("value")!.Value);

        Assert.Equal("设备与解决方案", values["ExplorerStepTitle"]);
        Assert.Contains("“设备控制”和“解决方案资源管理器”之间切换", values["ExplorerStepDescription"]);
        Assert.DoesNotContain("写入业务数据", values["WelcomeDescription"]);

        XDocument overlay = LoadGuideOverlay();
        XElement welcomeCard = Assert.Single(overlay.Descendants(), element => element.Attribute(Xaml + "Name")?.Value == "WelcomeCard");
        XElement tourCard = Assert.Single(overlay.Descendants(), element => element.Attribute(Xaml + "Name")?.Value == "TourCard");
        Assert.InRange(double.Parse(welcomeCard.Attribute("Width")!.Value), 360, 440);
        Assert.InRange(double.Parse(tourCard.Attribute("Width")!.Value), 300, 350);
    }

    [Fact]
    public void SpotlightMasksOnlyTheAreaOutsideTheTargetAndPlacesTheCardBesideIt()
    {
        NewUserGuideLayoutResult layout = NewUserGuideLayout.Calculate(
            new Size(1200, 800),
            new Rect(20, 100, 280, 560),
            new Size(372, 220));

        Assert.Equal(new Rect(12, 92, 296, 576), layout.Spotlight);
        Assert.True(layout.Card.Left > layout.Spotlight.Right);
        Assert.InRange(layout.Card.Right, 0, 1200);
        Assert.InRange(layout.Card.Bottom, 0, 800);

        double maskArea = Area(layout.TopMask) + Area(layout.LeftMask) + Area(layout.RightMask) + Area(layout.BottomMask);
        Assert.Equal(1200 * 800 - Area(layout.Spotlight), maskArea, 6);
    }

    [Fact]
    public void MissingTargetFallsBackToAFullMaskAndCenteredCard()
    {
        NewUserGuideLayoutResult layout = NewUserGuideLayout.Calculate(
            new Size(1000, 700),
            null,
            new Size(360, 200));

        Assert.True(layout.Spotlight.IsEmpty);
        Assert.Equal(new Rect(0, 0, 1000, 700), layout.TopMask);
        Assert.Equal(new Rect(320, 250, 360, 200), layout.Card);
    }

    [Fact]
    public void OverlaySupportsWelcomeStepNavigationCompletionAndDismissal()
    {
        WpfTestHost.Invoke(() =>
        {
            var target = new Border { Width = 180, Height = 100, Background = Brushes.Transparent };
            var overlay = new NewUserGuideOverlay();
            var host = new Grid { Width = 900, Height = 600 };
            host.Children.Add(target);
            host.Children.Add(overlay);
            var window = new Window
            {
                Content = host,
                Width = 900,
                Height = 600,
                Left = -16000,
                Top = -16000,
                ShowActivated = false,
                ShowInTaskbar = false,
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                bool dismissed = false;
                overlay.GuideDismissed += (_, _) => dismissed = true;
                overlay.ShowWelcome([new NewUserGuideStep("Title", "Description", () => target)]);
                Assert.True(overlay.IsGuideOpen);
                Assert.Equal(-1, overlay.CurrentStepIndex);

                overlay.StartTour();
                Assert.Equal(0, overlay.CurrentStepIndex);
                window.UpdateLayout();
                Point targetCenter = target.TranslatePoint(new Point(target.ActualWidth / 2, target.ActualHeight / 2), host);
                Assert.Same(target, host.InputHitTest(targetCenter));
                Assert.NotSame(target, host.InputHitTest(new Point(20, 20)));
                overlay.Next();
                Assert.Equal(1, overlay.CurrentStepIndex);
                overlay.Dismiss();

                Assert.True(dismissed);
                Assert.False(overlay.IsGuideOpen);
                Assert.Equal(-1, overlay.CurrentStepIndex);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void TourCardRemeasuresWhenTheNextStepNeedsMoreHeight()
    {
        WpfTestHost.Invoke(() =>
        {
            var target = new Border { Width = 180, Height = 100, Background = Brushes.Transparent };
            var overlay = new NewUserGuideOverlay();
            var host = new Grid { Width = 900, Height = 600 };
            host.Children.Add(target);
            host.Children.Add(overlay);
            var window = new Window
            {
                Content = host,
                Width = 900,
                Height = 600,
                Left = -16000,
                Top = -16000,
                ShowActivated = false,
                ShowInTaskbar = false,
            };

            try
            {
                window.Show();
                overlay.ShowWelcome(
                [
                    new NewUserGuideStep("Short", "One line.", () => target),
                    new NewUserGuideStep("Long", string.Join(' ', Enumerable.Repeat("A longer professional description.", 12)), () => target),
                ]);
                overlay.StartTour();
                window.UpdateLayout();
                double shortHeight = ((FrameworkElement)overlay.FindName("TourCard")).ActualHeight;

                overlay.Next();
                window.UpdateLayout();
                double longHeight = ((FrameworkElement)overlay.FindName("TourCard")).ActualHeight;

                Assert.True(longHeight > shortHeight);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static double Area(Rect rect) => rect.IsEmpty ? 0 : rect.Width * rect.Height;

    private static XDocument LoadMainWindow([CallerFilePath] string testPath = "") =>
        XDocument.Load(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testPath)!, "..", "..", "ColorVision", "MainWindow.xaml")));

    private static XDocument LoadGuideOverlay([CallerFilePath] string testPath = "") =>
        XDocument.Load(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testPath)!, "..", "..", "ColorVision", "Guidance", "NewUserGuideOverlay.xaml")));

    private static string GetTestPath([CallerFilePath] string testPath = "") => testPath;
}
