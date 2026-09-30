using ColorVision.Themes;
using ColorVision.UI.Desktop.LanRemote;
using ColorVision.UI.Desktop.Operations;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class LanRemoteSettingsPresentationTests
{
    [Theory]
    [InlineData("zh-Hans", "连接手机", "设备管理", "本机确认", "网络设置")]
    [InlineData("en-US", "Connect phone", "Devices", "Local approval", "Network")]
    [InlineData("zh-Hant", "連接手機", "裝置管理", "本機確認", "網路設定")]
    public void AllSectionsUseTheSelectedLanguageAndKeepAccessActionsDisabledUntilSelection(string culture, params string[] headers)
    {
        WpfTestHost.Invoke(() =>
        {
            CultureInfo previous = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                using var resources = new PresentationResources(false);
                var control = new LanRemoteControlSettingsControl();
                // No Window/Loaded: rendering a settings view must not start listeners or alter real pairing data.
                var tabs = (TabControl)control.FindName("SectionsTabControl");
                Assert.Equal(headers, tabs.Items.Cast<TabItem>().Select(item => item.Header is TextBlock label ? label.Text : item.Header.ToString()));
                foreach (TabItem tab in tabs.Items)
                {
                    tabs.SelectedItem = tab;
                    control.Measure(new Size(820, double.PositiveInfinity));
                    control.Arrange(new Rect(0, 0, 820, control.DesiredSize.Height));
                    control.UpdateLayout();
                    string[] labels = Descendants(control).OfType<TextBlock>().Select(item => item.Text).Where(text => !string.IsNullOrEmpty(text)).ToArray();
                    Assert.NotEmpty(labels);
                    Assert.DoesNotContain(labels, label => label.StartsWith("LanRemote_", StringComparison.Ordinal));
                    if (culture == "en-US")
                        Assert.DoesNotContain(labels, label => label.Any(character => character is >= '\u4e00' and <= '\u9fff'));
                }
                var pending = (ListBox)control.FindName("PendingDevicesListBox");
                var approve = (Button)control.FindName("ApproveDeviceButton");
                Assert.False(approve.IsEnabled);
                var claim = new OperationsPairingClaim { DeviceName = "用户设备", DeviceId = "device-a", PairingId = "pair-a" };
                pending.Items.Add(claim);
                pending.SelectedItem = claim;
                Assert.True(approve.IsEnabled);
                Assert.Same(claim, pending.SelectedItem);
                Assert.Equal("用户设备", claim.DeviceName);
                pending.SelectedItem = null;
                Assert.False(approve.IsEnabled);
            }
            finally { CultureInfo.CurrentUICulture = previous; }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CardsReflowInBothThemesWithoutLosingTheirContent(bool dark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var resources = new PresentationResources(dark);
            var control = new LanRemoteControlSettingsControl();
            foreach (int width in new[] { 820, 480, 820 })
            {
                control.Measure(new Size(width, double.PositiveInfinity));
                control.Arrange(new Rect(0, 0, width, control.DesiredSize.Height));
                control.UpdateLayout();
                var pairingCard = (Border)control.FindName("PairingCard");
                Assert.Equal(width < 720 ? 1 : 0, Grid.GetRow(pairingCard));
                Assert.Equal(width < 720 ? 0 : 2, Grid.GetColumn(pairingCard));
                Assert.True(pairingCard.ActualWidth >= 208);
                Assert.True(((Grid)control.FindName("ConnectionGrid")).ActualWidth <= width);
            }
        });
    }

    [Fact]
    public void LocalizedJobAndSupportLabelsPreserveProtocolFieldsAndCallerText()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            var converter = new LanRemoteItemTextConverter();
            var job = new OperationsJob { CapabilityId = "ops.window.snapshot.capture", Reason = "用户输入" };
            Assert.Equal("Capture a ColorVision main window snapshot", converter.Convert(job, typeof(string), "Title", CultureInfo.CurrentUICulture));
            Assert.Contains("5 minutes", (string)converter.Convert(job, typeof(string), "Notice", CultureInfo.CurrentUICulture));
            Assert.Equal("ops.window.snapshot.capture", job.CapabilityId);
            Assert.Equal("用户输入", job.Reason);
            var support = new OperationsSupportSession { Mode = "guided" };
            Assert.Equal("Guided support", converter.Convert(support, typeof(string), "", CultureInfo.CurrentUICulture));
            Assert.Equal("guided", support.Mode);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class PresentationResources : IDisposable
    {
        private readonly List<ResourceDictionary> _dictionaries = [];
        public PresentationResources(bool dark)
        {
            foreach (string uri in (dark ? ThemeManager.ResourceDictionaryDark : ThemeManager.ResourceDictionaryWhite).Concat(ThemeManager.ResourceDictionaryBase))
            {
                var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                _dictionaries.Add(dictionary);
                Application.Current.Resources.MergedDictionaries.Add(dictionary);
            }
        }
        public void Dispose()
        {
            foreach (var dictionary in _dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
        }
    }
}
