using ColorVision.Solution.Editor.AvalonEditor;
using ColorVision.Themes;
using ICSharpCode.AvalonEdit;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class TextEditorSearchRenderingTests
{
    [Fact]
    public void SearchPaintsMatchesAcrossTheViewportAndUpdatesMinimapMarkers()
    {
        WpfTestHost.Invoke(() =>
        {
            var resources = Application.Current.Resources.MergedDictionaries;
            var dictionaries = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
                .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) }).ToArray();
            foreach (var dictionary in dictionaries) resources.Add(dictionary);
            Window? window = null;
            try
            {
                using var owner = new AvalonEditControll(new EditorPreferences { ShowMinimap = true, ShowPreview = false });
                var editor = Assert.IsType<TextEditor>(owner.FindName("textEditor"));
                var search = Assert.IsType<EditorSearchBar>(owner.FindName("SearchBar"));
                var map = Assert.IsType<EditorMinimap>(owner.FindName("Minimap"));
                editor.Text = "BEGIN\n" + string.Concat(Enumerable.Repeat("content\n", 400)) + "END";
                window = new Window { Content = owner, Width = 860, Height = 640, Left = -16000, ShowInTaskbar = false, ShowActivated = false };
                window.Show();
                UpdateLayout(window);

                search.Open(false);
                Assert.IsType<CheckBox>(search.FindName("RegexBox")).IsChecked = true;
                var query = Assert.IsType<TextBox>(search.FindName("QueryBox"));
                query.Text = @"BEGIN[\s\S]*END";
                search.FindNext();
                editor.ScrollToLine(200);
                UpdateLayout(window);
                var view = editor.TextArea.TextView;
                int firstOffset = view.VisualLines[0].FirstDocumentLine.Offset;
                var match = Assert.Single(search.Matches);
                Assert.True(match.Index < firstOffset && match.Index + match.Length > firstOffset);
                var highlights = new DrawingGroup();
                using (var dc = highlights.Open()) search.Draw(view, dc);
                Assert.NotEmpty(highlights.Children);

                query.Text = "(?=content)";
                search.FindNext();
                editor.ScrollToLine(200);
                UpdateLayout(window);
                Assert.NotEmpty(search.Matches);
                Assert.All(search.Matches, item => Assert.Equal(0, item.Length));
                using (var dc = highlights.Open()) search.Draw(view, dc);
                Assert.Empty(highlights.Children);

                map.SetSearchLines([1, 2, 200]);
                UpdateLayout(window);
                double[] beforeResize = MarkerPositions(map);
                Assert.Equal(new[] { 0d, Math.Floor(199 * map.ActualHeight / editor.Document.LineCount) }, beforeResize);
                window.Height = 480;
                UpdateLayout(window);
                double[] afterResize = MarkerPositions(map);
                Assert.Equal(new[] { 0d, Math.Floor(199 * map.ActualHeight / editor.Document.LineCount) }, afterResize);
                Assert.NotEqual(beforeResize[1], afterResize[1]);

                map.SetSearchLines([300]);
                UpdateLayout(window);
                Assert.Equal(Math.Floor(299 * map.ActualHeight / editor.Document.LineCount), Assert.Single(MarkerPositions(map)));
            }
            finally
            {
                window?.Close();
                foreach (var dictionary in dictionaries) resources.Remove(dictionary);
            }
        });
    }

    private static double[] MarkerPositions(EditorMinimap map) => VisualTreeHelper.GetDrawing(map).Children.OfType<GeometryDrawing>()
        .Select(drawing => (drawing.Geometry as RectangleGeometry)?.Rect)
        .Where(rect => rect is { Width: 5, Height: 2 })
        .Select(rect => rect!.Value.Y)
        .ToArray();

    private static void UpdateLayout(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
}
