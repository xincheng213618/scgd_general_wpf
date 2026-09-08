using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public class GridViewColumnHeaderBindingTests
{
    [Fact]
    public void DetachedHeader_PreservesItsTemplateWithoutMissingListViewBinding()
    {
        WpfTestHost.Invoke(() => WithThemeResources(() =>
        {
            using var trace = new BindingTrace();
            var header = new GridViewColumnHeader { Content = "Result", FontSize = 18 };

            Arrange(header);

            Assert.Equal(0, header.MinHeight);
            Assert.True(header.DesiredSize.Height > 0);
            Assert.Equal(18, header.FontSize);
            Assert.IsType<Thumb>(header.Template.FindName("PART_HeaderGripper", header));
            Assert.DoesNotContain("GridViewColumnHeader", trace.Output);
        }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ListViewHeaders_KeepFontInheritanceAndResizeTemplates(bool useCustomHeaderStyle)
    {
        WpfTestHost.Invoke(() => WithThemeResources(() =>
        {
            using var trace = new BindingTrace();
            var columns = new GridView();
            columns.Columns.Add(new GridViewColumn { Header = "Name", Width = 140 });
            columns.Columns.Add(new GridViewColumn { Header = "Result", Width = 180 });
            if (useCustomHeaderStyle)
                columns.ColumnHeaderContainerStyle = (Style)Application.Current.FindResource("GridViewColumnHeaderBase");

            var listView = new ListView { View = columns, FontSize = 18 };
            listView.Items.Add("sample");
            Arrange(listView);

            var headers = VisualDescendants<GridViewColumnHeader>(listView)
                .Where(header => header.Role == GridViewColumnHeaderRole.Normal && header.Column != null)
                .ToArray();
            Assert.Equal(2, headers.Length);
            Assert.All(headers, header =>
            {
                Assert.Equal(18, header.FontSize);
                Assert.IsType<Thumb>(header.Template.FindName("PART_HeaderGripper", header));
            });

            listView.FontSize = 22;
            Arrange(listView);

            Assert.All(headers, header => Assert.Equal(22, header.FontSize));
            Assert.DoesNotContain("GridViewColumnHeader", trace.Output);
        }));
    }

    private static void Arrange(FrameworkElement element)
    {
        element.Measure(new Size(600, 400));
        element.Arrange(new Rect(0, 0, 600, 400));
        element.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static IEnumerable<T> VisualDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                yield return match;
            foreach (T descendant in VisualDescendants<T>(child))
                yield return descendant;
        }
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

        public string Output => _writer.ToString();

        public void Dispose()
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(_listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = _previousLevel;
            _listener.Dispose();
            _writer.Dispose();
        }
    }
}
