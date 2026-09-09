using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Themes;
using ColorVision.UI.PropertyEditor.Json;
using ICSharpCode.AvalonEdit;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public class JsonTemplateEditorTests
{
    private sealed class Parameter(string json) : IEditTemplateJson
    {
        public string JsonValue { get; set; } = json;
        public RelayCommand ResetCommand { get; set; } = new(_ => { });
        public RelayCommand CheckCommand { get; set; } = new(_ => { });
        public event EventHandler? JsonValueChanged;
        public void Replace(string value) { JsonValue = value; JsonValueChanged?.Invoke(this, EventArgs.Empty); }
    }

    [Fact]
    public void InvalidDocumentClearsPreviousFields()
    {
        WpfTestHost.Invoke(() =>
        {
            var editor = new JsonPropertyEditorControl();
            editor.SetJson("{\"old\": 1}");
            editor.SetJson("{broken");
            Assert.False(editor.CanEdit);
            Assert.False(editor.ValidateJson());
            Assert.DoesNotContain(Descendants(editor).OfType<TextBox>(), box => box.Tag is string);
            Assert.Equal("{broken", editor.GetJson());
        });
    }

    [Fact]
    public void TypedInputsPreserveLargeIntegersLiteralKeysAndArrayElements()
    {
        WpfTestHost.Invoke(() =>
        {
            var editor = new JsonPropertyEditorControl();
            editor.SetJson("""{"a.b":1,"large":9223372036854775808,"items":["a,b","c"],"date":"2026-09-10T00:00:00Z"}""");
            Field(editor, "['a.b']").Text = "7";
            Field(editor, "large").Text = "9223372036854775809";
            var arrayBox = Field(editor, "items");
            arrayBox.Text = """["a,b", broken]""";
            Assert.False(editor.ValidateJson());
            Assert.Equal(new[] { "a,b", "c" }, JObject.Parse(editor.GetJson()!)["items"]!.Values<string>());
            arrayBox.Text = """["a,b", "d", ""]""";
            Assert.True(editor.ValidateJson());
            var result = JObject.Parse(editor.GetJson()!);
            Assert.Equal(7, result["a.b"]!.Value<int>());
            Assert.Equal("9223372036854775809", result["large"]!.ToString());
            Assert.Equal(new[] { "a,b", "d", "" }, result["items"]!.Values<string>());
            // The property parser leaves ISO-looking strings as strings.
            Assert.Contains("\"date\": \"2026-09-10T00:00:00Z\"", editor.GetJson());
        });
    }

    [Fact]
    public void InvalidRangesAndNonJsonArrayValuesRemainDrafts()
    {
        WpfTestHost.Invoke(() =>
        {
            var editor = new JsonPropertyEditorControl();
            editor.SetJson("""{"gain":2.0,"values":[1,2],"flags":[false,true,false]}""",
                """{"properties":{"gain":{"minimum":0,"maximum":5}}}""");
            Field(editor, "gain").Text = "8";
            Assert.False(editor.ValidateJson());
            Assert.Equal(2, JObject.Parse(editor.GetJson()!)["gain"]!.Value<double>());
            Field(editor, "gain").Text = "4.5";
            Field(editor, "values").Text = "[1,NaN]";
            Assert.False(editor.ValidateJson());
            Assert.Equal(new[] { 1, 2 }, JObject.Parse(editor.GetJson()!)["values"]!.Values<int>());
            Field(editor, "values").Text = "[3,4]";
            var flag = Assert.Single(Descendants(editor).OfType<CheckBox>(), box => Equals(box.ToolTip, "flags[2]"));
            flag.IsChecked = true;
            Assert.True(editor.ValidateJson());
            Assert.Equal(new[] { false, true, true }, JObject.Parse(editor.GetJson()!)["flags"]!.Values<bool>());
        });
    }

    [Fact]
    public void LongTemplateUsesGroupsAndGlobalSearchWithoutLosingInvalidDraft()
    {
        WpfTestHost.Invoke(() =>
        {
            var document = new JObject();
            for (int group = 0; group < 5; group++)
                document[$"group{group}"] = new JObject(Enumerable.Range(0, 10).Select(i => new JProperty($"value{i}", i)));
            var editor = new JsonPropertyEditorControl();
            editor.SetJson(document.ToString());
            Assert.Equal(0, Part<ComboBox>(editor, "GroupSelector").SelectedIndex);
            Assert.Equal(50, Descendants(editor).OfType<TextBox>().Count(box => box.Tag is string));
            Part<ComboBox>(editor, "GroupSelector").SelectedIndex = 1;
            Assert.Equal(10, Descendants(editor).OfType<TextBox>().Count(box => box.Tag is string));
            Field(editor, "group0.value0").Text = "-";
            Part<TextBox>(editor, "SearchTextBox").Text = "group4.value9";
            Assert.Equal("9", Field(editor, "group4.value9").Text);
            Assert.False(editor.ValidateJson());
            Part<TextBox>(editor, "SearchTextBox").Text = "";
            Assert.Equal("-", Field(editor, "group0.value0").Text);
            Field(editor, "group0.value0").Text = "-12";
            Assert.True(editor.ValidateJson());
            Assert.Equal(-12, JObject.Parse(editor.GetJson()!).SelectToken("group0.value0")!.Value<int>());
            var selector = Part<ComboBox>(editor, "GroupSelector");
            selector.SelectedIndex = 4;
            editor.SetJson(editor.GetJson()!);
            Assert.Equal(4, selector.SelectedIndex);
        });
    }

    [Fact]
    public void PropertyErrorsAndTextErrorsBlockCommitAndModeSwitchUntilCorrected()
    {
        WithTemplate(editor =>
        {
            var parameter = new Parameter("{\"value\":1}");
            editor.SetParam(parameter);
            var properties = Part<JsonPropertyEditorControl>(editor, "propertyEditor");
            Field(properties, "value").Text = "-";
            Click(editor, "TextModeButton");
            Assert.Equal(Visibility.Visible, properties.Visibility);
            Assert.False(editor.TryCommitPendingEdits());
            Field(properties, "value").Text = "23";
            Assert.True(editor.TryCommitPendingEdits());
            Assert.Equal(23, JObject.Parse(parameter.JsonValue)["value"]!.Value<int>());
            Click(editor, "TextModeButton");
            var text = Part<TextEditor>(editor, "textEditor");
            text.Text = "{\"value\":";
            Click(editor, "ValidateButton");
            Click(editor, "PropertyModeButton");
            Assert.Equal("{\"value\":", text.Text);
            Assert.Equal(Visibility.Visible, text.Visibility);
            Assert.False(editor.TryCommitPendingEdits());
            text.Text = "{\"value\":42}";
            Assert.True(editor.TryCommitPendingEdits());
            Assert.Equal(42, JObject.Parse(parameter.JsonValue)["value"]!.Value<int>());
        });
    }

    [Fact]
    public void ModelRefreshUpdatesBothViewsAndEditorsDoNotSharePendingText()
    {
        WithTemplate(editor =>
        {
            var parameter = new Parameter("{\"value\":1}");
            editor.SetParam(parameter);
            parameter.Replace("{\"value\":77}");
            Assert.Equal("77", Field(Part<JsonPropertyEditorControl>(editor, "propertyEditor"), "value").Text);
            Click(editor, "TextModeButton");
            var other = new EditTemplateJson("") { Height = double.NaN, Width = double.NaN };
            var otherParameter = new Parameter("{\"value\":2}");
            other.SetParam(otherParameter);
            Part<TextEditor>(editor, "textEditor").Text = "{\"value\":101}";
            Part<TextEditor>(other, "textEditor").Text = "{\"value\":202}";
            Assert.Equal(101, JObject.Parse(parameter.JsonValue)["value"]!.Value<int>());
            Assert.Equal(202, JObject.Parse(otherParameter.JsonValue)["value"]!.Value<int>());
            editor.AcceptSavedChanges();
            Assert.Contains("无新修改", Part<TextBlock>(editor, "StatusText").Text);
        });
    }

    [Fact]
    public void RootArrayRemainsInTextModeAndFormattingCanBeUndone()
    {
        WithTemplate(editor =>
        {
            var parameter = new Parameter("[1,2,3]");
            editor.SetParam(parameter);
            var text = Part<TextEditor>(editor, "textEditor");
            Assert.Equal(Visibility.Visible, text.Visibility);
            Click(editor, "PropertyModeButton");
            Assert.Equal(Visibility.Visible, text.Visibility);
            Click(editor, "FormatButton");
            Assert.Contains("\n", text.Text);
            text.Undo();
            Assert.Equal("[1,2,3]", text.Text);
            Assert.True(editor.TryCommitPendingEdits());
            editor.AcceptSavedChanges();
            Assert.Contains("无新修改", Part<TextBlock>(editor, "StatusText").Text);
        });
    }

    [Fact]
    public void GhostTemplateRendersInBothThemesAndTextMode()
    {
        WpfTestHost.Invoke(() =>
        {
            string root = FindRepository();
            string schema = File.ReadAllText(Path.Combine(root, "Engine/ColorVision.Engine/Templates/Jsons/Ghost2/ghost.schema.json"));
            string json = JObject.Parse(schema)["default"]!.ToString();
            foreach (bool dark in new[] { false, true })
                WithTemplate(editor =>
                {
                    editor.SetParam(new Parameter(json));
                    Part<JsonPropertyEditorControl>(editor, "propertyEditor").SetJson(json, schema, "鬼影 V2");
                    Part<TextBlock>(editor, "EditorTitleText").Text = "ghost1";
                    Part<TextBlock>(editor, "EditorSubtitleText").Text = "鬼影 V2  ·  ghost";
                    var window = Window.GetWindow(editor)!;
                    Drain();
                    var properties = Part<JsonPropertyEditorControl>(editor, "propertyEditor");
                    Assert.Equal(Visibility.Collapsed, Part<Border>(properties, "DetailsPanel").Visibility);
                    Capture(editor, $"parameters-{(dark ? "dark" : "light")}.png");
                    Part<ToggleButton>(properties, "DetailsToggle").IsChecked = true;
                    Drain();
                    Assert.Equal(Visibility.Visible, Part<Border>(properties, "DetailsPanel").Visibility);
                    Part<ToggleButton>(properties, "DetailsToggle").IsChecked = false;
                    Click(editor, "TextModeButton");
                    Drain();
                    Capture(editor, $"json-{(dark ? "dark" : "light")}.png");
                    // The surface should remain usable at its supported narrow width too.
                    window.Width = 520;
                    window.UpdateLayout();
                    Click(editor, "PropertyModeButton");
                    Part<JsonPropertyEditorControl>(editor, "propertyEditor").SetJson(json, schema, "鬼影 V2");
                    Drain();
                    Assert.True(editor.TryCommitPendingEdits());
                    Capture(editor, $"narrow-{(dark ? "dark" : "light")}.png");
                }, dark);
        });
    }

    private static void WithTemplate(Action<EditTemplateJson> action, bool dark = false)
    {
        WpfTestHost.Invoke(() =>
        {
            var previous = ConfigService.Instance;
            ConfigService.SetInstance(new ConfigHandler());
            EditTemplateJsonConfig.Instance = new EditTemplateJsonConfig { UsePropertyEditor = true };
            var window = new Window { Width = 840, Height = 760, Left = -16000, Top = -16000, ShowInTaskbar = false, ShowActivated = false };
            foreach (string source in (dark ? ThemeManager.ResourceDictionaryDark : ThemeManager.ResourceDictionaryWhite).Concat(ThemeManager.ResourceDictionaryBase))
                window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.RelativeOrAbsolute) });
            // Button style is resolved at construction before attaching to the window.
            var editor = new EditTemplateJson("") { Width = double.NaN, Height = double.NaN };
            window.Content = editor;
            try { window.Show(); window.UpdateLayout(); action(editor); }
            finally { window.Close(); ConfigService.SetInstance(previous!); }
        });
    }

    private static string FindRepository()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "build.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static void Capture(FrameworkElement visual, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("JSON_EDITOR_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        visual.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name));
        encoder.Save(stream);
    }

    private static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static T Part<T>(FrameworkElement root, string name) => Assert.IsAssignableFrom<T>(root.FindName(name));
    private static void Click(FrameworkElement root, string name) => Part<ButtonBase>(root, name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    private static TextBox Field(FrameworkElement root, string path) => Assert.Single(Descendants(root).OfType<TextBox>(), box => Equals(box.Tag, path));
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
