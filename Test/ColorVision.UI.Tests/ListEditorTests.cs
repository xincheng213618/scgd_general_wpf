#pragma warning disable CA1707,CA1711,CA1852
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorVision.Engine.FlowProcessing.PreProcess;
using ColorVision.Themes;
using ColorVision.UI.PropertyEditor.Editor.List;
using FlowEngineLib.Node.Algorithm;

namespace ColorVision.UI.Tests;

public class ListEditorTests
{
    public enum TestEnum
    {
        Value1,
        Value2,
        Value3
    }

    [Fact]
    public void JsonNumericListConverter_ConvertBack_WithEnumArray_ReturnsArray()
    {
        var converter = new JsonNumericListConverter();

        var result = Assert.IsType<TestEnum[]>(converter.ConvertBack("[0,2]", typeof(TestEnum[]), null!, CultureInfo.InvariantCulture));

        Assert.Equal(new[] { TestEnum.Value1, TestEnum.Value3 }, result);
    }

    [Fact]
    public void JsonNumericListConverter_ConvertBack_WithPointFloatArray_ReturnsArray()
    {
        var converter = new JsonNumericListConverter();

        var result = Assert.IsType<PointFloat[]>(converter.ConvertBack("[{\"X\":1.5,\"Y\":2.25},{\"X\":3.5,\"Y\":4.25}]", typeof(PointFloat[]), null!, CultureInfo.InvariantCulture));

        Assert.Equal(2, result.Length);
        Assert.Equal(1.5f, result[0].X);
        Assert.Equal(2.25f, result[0].Y);
        Assert.Equal(3.5f, result[1].X);
        Assert.Equal(4.25f, result[1].Y);
    }

    [Fact]
    public void CacheFolders_DeclareFolderItemEditor()
    {
        var property = typeof(FolderSizePreProcessorConfig).GetProperty(nameof(FolderSizePreProcessorConfig.FolderPaths))!;
        Assert.Equal(typeof(TextSelectFolderPropertiesEditor), property.GetCustomAttribute<CollectionEditorTypeAttribute>()?.ItemEditorType);
    }

    [Theory]
    [InlineData(null, 0, false)]
    [InlineData(typeof(TextboxPropertiesEditor), 0, false)]
    [InlineData(typeof(TextSelectFolderPropertiesEditor), 2, false)]
    [InlineData(typeof(TextSelectFolderPropertiesEditor), 2, true)]
    [InlineData(typeof(TextSelectFilePropertiesEditor), 2, false)]
    public void StringItems_EditInlineAndCancelPreservesOriginal(Type? editorType, int actionCount, bool dark)
    {
        var original = new List<string> { @"C:\Cache" };
        WithDialog(original, typeof(string), editorType, window =>
        {
            var list = (ListBox)window.FindName("ItemsListBox");
            Assert.Equal(Visibility.Collapsed, ((Button)window.FindName("EditButton")).Visibility);
            var row = Assert.IsType<ListEditorWindow.ListItemViewModel>(list.Items[0]);
            var editor = Assert.IsType<DockPanel>(row.InlineEditor);
            Assert.Equal(actionCount, editor.Children.OfType<Button>().Count());
            Assert.Empty(editor.Children.OfType<ComboBox>());
            Assert.Empty(editor.Children.OfType<TextBlock>());
            var textBox = Assert.Single(editor.Children.OfType<TextBox>());
            textBox.Text = @"D:\Edited";
            Assert.Equal(@"D:\Edited", row.Value);
            Assert.Equal(new[] { @"C:\Cache" }, original);
            if (editorType == typeof(TextSelectFolderPropertiesEditor))
            {
                Capture(window, $"folders-{(dark ? "dark" : "light")}.png");
                window.Width = window.MinWidth;
                window.UpdateLayout();
                Assert.True(textBox.IsLoaded);
                Assert.True(textBox.ActualWidth > 0);
                foreach (var button in editor.Children.OfType<Button>())
                    Assert.True(button.TransformToAncestor(list).Transform(new Point(button.ActualWidth, 0)).X <= list.ActualWidth);
                Capture(window, $"folders-narrow-{(dark ? "dark" : "light")}.png");
            }
            Click(window, "CancelButton");
        }, dark);
        Assert.Equal(new[] { @"C:\Cache" }, original);
    }

    [Fact]
    public void InlineAddAndReorder_PreserveEditsAndCommitInOrder()
    {
        var original = new List<string> { @"C:\First", @"C:\Second" };
        WithDialog(original, typeof(string), typeof(TextSelectFolderPropertiesEditor), window =>
        {
            var list = (ListBox)window.FindName("ItemsListBox");
            Click(window, "AddButton");
            Assert.Equal(3, list.Items.Count);
            Assert.Equal(2, list.SelectedIndex);
            var added = Assert.IsType<ListEditorWindow.ListItemViewModel>(list.SelectedItem);
            var textBox = Assert.Single(added.InlineEditor!.Children.OfType<TextBox>());
            textBox.Text = @"D:\New";
            Click(window, "MoveUpButton");
            Assert.Equal(1, list.SelectedIndex);
            Assert.Equal(@"D:\New", ((ListEditorWindow.ListItemViewModel)list.SelectedItem).Value);
            Click(window, "MoveDownButton");
            Assert.Equal(2, list.SelectedIndex);
            Assert.Equal(@"D:\New", ((ListEditorWindow.ListItemViewModel)list.SelectedItem).Value);
            Click(window, "MoveUpButton");
            Assert.Equal(new[] { @"C:\First", @"C:\Second" }, original);
            Click(window, "OkButton");
        });
        Assert.Equal(new[] { @"C:\First", @"D:\New", @"C:\Second" }, original);
    }

    [Fact]
    public void NumericItems_KeepItemEditorWithoutIndexColumn()
    {
        WithDialog(new List<int> { 1, 2 }, typeof(int), null, window =>
        {
            var list = (ListBox)window.FindName("ItemsListBox");
            Assert.Null(((ListEditorWindow.ListItemViewModel)list.Items[0]).InlineEditor);
            list.SelectedIndex = 0;
            var edit = (Button)window.FindName("EditButton");
            Assert.Equal(Visibility.Visible, edit.Visibility);
            Assert.True(edit.IsEnabled);
            Click(window, "CancelButton");
        });
    }

    private static void Click(Window window, string name)
        => ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void WithDialog(System.Collections.IList items, Type elementType, Type? editorType, Action<ListEditorWindow> action, bool dark = false)
    {
        WpfTestHost.Invoke(() =>
        {
            var previousResources = Application.Current.Resources;
            var previousTheme = ThemeManager.Current.CurrentTheme ?? Theme.Light;
            Application.Current.Resources = new ResourceDictionary();
            Application.Current.ForceApplyTheme(dark ? Theme.Dark : Theme.Light);
            ListEditorWindow? window = null;
            try
            {
                window = new ListEditorWindow(items, elementType, editorType)
                {
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -10000,
                    Top = -10000
                };
                Exception? failure = null;
                window.ContentRendered += (_, _) =>
                {
                    try { action(window); }
                    catch (Exception ex) { failure = ex; }
                    finally { window.Close(); }
                };
                window.ShowDialog();
                if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            }
            finally
            {
                window?.Close();
                Application.Current.Resources = previousResources;
                Application.Current.ForceApplyTheme(previousTheme);
            }
        });
    }

    private static void Capture(Window window, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("LIST_EDITOR_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var visual = (FrameworkElement)window.Content;
        visual.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var drawing = background.RenderOpen())
        {
            var bounds = new Rect(0, 0, visual.ActualWidth, visual.ActualHeight);
            drawing.DrawRectangle(window.Background, null, bounds);
            drawing.DrawRectangle(new VisualBrush(visual), null, bounds);
        }
        bitmap.Render(background);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name));
        encoder.Save(stream);
    }

}
