using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class ComboBoxItemBindingTests
{
    [Theory]
    [InlineData("ComboBoxItemBaseStyle")]
    [InlineData("ComboBoxItem.Small")]
    public void DetachedItems_KeepTheirTemplateWithoutMissingAncestorBindings(string styleKey)
    {
        WpfTestHost.Invoke(() => WithThemeResources(() =>
        {
            using var trace = new BindingTrace();
            var item = new ComboBoxItem
            {
                Content = "Detached item",
                Style = (Style)Application.Current.FindResource(styleKey),
            };

            item.Measure(new Size(240, 40));
            item.Arrange(new Rect(0, 0, 240, 40));
            item.UpdateLayout();
            PumpDispatcher();

            Assert.Equal(HorizontalAlignment.Left, item.HorizontalContentAlignment);
            Assert.Equal(VerticalAlignment.Center, item.VerticalContentAlignment);
            Assert.NotNull(item.Template);
            Assert.True(item.DesiredSize.Height > 0);
            trace.AssertNoAlignmentFailures();
        }));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("ComboBox.Small", false)]
    [InlineData("ComboBoxExtend.Small", false)]
    [InlineData("ComboBoxPlus.Small", true)]
    [InlineData("ComboBoxBaseStyle", false)]
    public void VisibleItems_FollowOwnerAlignmentAndChanges(string? styleKey, bool useHandyControl)
    {
        WpfTestHost.Invoke(() => WithThemeResources(() =>
        {
            using var trace = new BindingTrace();
            ComboBox combo = useHandyControl ? new HandyControl.Controls.ComboBox() : new ComboBox();
            if (styleKey != null)
                combo.Style = (Style)Application.Current.FindResource(styleKey);
            combo.HorizontalContentAlignment = HorizontalAlignment.Right;
            combo.VerticalContentAlignment = VerticalAlignment.Bottom;
            combo.ItemsSource = new[] { "First", "Second" };

            WithVisiblePopup(combo, () =>
            {
                var item = Assert.IsType<ComboBoxItem>(combo.ItemContainerGenerator.ContainerFromIndex(0));
                Assert.True(item.IsVisible);
                Assert.Equal(HorizontalAlignment.Right, item.HorizontalContentAlignment);
                Assert.Equal(VerticalAlignment.Bottom, item.VerticalContentAlignment);

                combo.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                combo.VerticalContentAlignment = VerticalAlignment.Top;
                PumpDispatcher();

                Assert.Equal(HorizontalAlignment.Stretch, item.HorizontalContentAlignment);
                Assert.Equal(VerticalAlignment.Top, item.VerticalContentAlignment);
                Assert.NotNull(item.Template);
            });
            trace.AssertNoAlignmentFailures();
        }));
    }

    [Fact]
    public void ClosingAndRefreshingItems_RetainsAlignmentWithoutDetachedAncestorBindings()
    {
        WpfTestHost.Invoke(() => WithThemeResources(() =>
        {
            using var trace = new BindingTrace();
            var combo = new ComboBox
            {
                Style = (Style)Application.Current.FindResource("ComboBox.Small"),
                HorizontalContentAlignment = HorizontalAlignment.Right,
                VerticalContentAlignment = VerticalAlignment.Bottom,
                ItemsSource = new[] { "First", "Second" },
            };

            WithVisiblePopup(combo, () =>
            {
                var first = Assert.IsType<ComboBoxItem>(combo.ItemContainerGenerator.ContainerFromIndex(0));
                combo.IsDropDownOpen = false;
                PumpDispatcher();

                Assert.False(first.IsVisible);
                Assert.Equal(HorizontalAlignment.Left, first.HorizontalContentAlignment);
                Assert.Equal(VerticalAlignment.Center, first.VerticalContentAlignment);

                combo.ItemsSource = new[] { "Replacement" };
                combo.IsDropDownOpen = true;
                PumpDispatcher();
                var replacement = Assert.IsType<ComboBoxItem>(combo.ItemContainerGenerator.ContainerFromIndex(0));
                Assert.True(replacement.IsVisible);
                Assert.Equal(HorizontalAlignment.Right, replacement.HorizontalContentAlignment);
                Assert.Equal(VerticalAlignment.Bottom, replacement.VerticalContentAlignment);

                combo.ItemsSource = null;
                PumpDispatcher();

                Assert.False(replacement.IsVisible);
                Assert.Equal(HorizontalAlignment.Left, replacement.HorizontalContentAlignment);
                Assert.Equal(VerticalAlignment.Center, replacement.VerticalContentAlignment);
            });
            trace.AssertNoAlignmentFailures();
        }));
    }

    private static void WithVisiblePopup(ComboBox combo, Action action)
    {
        var window = new Window
        {
            Content = combo,
            Width = 280,
            Height = 100,
            Left = -10000,
            Top = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
        };
        try
        {
            window.Show();
            combo.IsDropDownOpen = true;
            PumpDispatcher();
            action();
        }
        finally
        {
            combo.IsDropDownOpen = false;
            window.Close();
            PumpDispatcher();
        }
    }

    private static void PumpDispatcher()
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void WithThemeResources(Action action)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var added = new List<ResourceDictionary>();
        try
        {
            foreach (string path in new[]
            {
                "/HandyControl;component/Themes/basic/colors/colors.xaml",
                "/HandyControl;component/Themes/Theme.xaml",
                "/ColorVision.Themes;component/Themes/White.xaml",
                "/ColorVision.Themes;component/Themes/Base.xaml",
            })
            {
                var dictionary = (ResourceDictionary)Application.LoadComponent(new Uri(path, UriKind.Relative));
                dictionaries.Add(dictionary);
                added.Add(dictionary);
            }
            action();
        }
        finally
        {
            foreach (ResourceDictionary dictionary in added)
                dictionaries.Remove(dictionary);
        }
    }

    private sealed class BindingTrace : IDisposable
    {
        private readonly StringWriter _writer = new();
        private readonly TextWriterTraceListener _listener;
        private readonly SourceLevels _previousLevel;

        public BindingTrace()
        {
            _previousLevel = PresentationTraceSources.DataBindingSource.Switch.Level;
            PresentationTraceSources.Refresh();
            _listener = new TextWriterTraceListener(_writer);
            PresentationTraceSources.DataBindingSource.Listeners.Add(_listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        }

        public void AssertNoAlignmentFailures()
        {
            string output = _writer.ToString();
            Assert.DoesNotContain("BindingExpression:Path=HorizontalContentAlignment", output);
            Assert.DoesNotContain("BindingExpression:Path=VerticalContentAlignment", output);
        }

        public void Dispose()
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(_listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = _previousLevel;
            _listener.Dispose();
            _writer.Dispose();
        }
    }
}
