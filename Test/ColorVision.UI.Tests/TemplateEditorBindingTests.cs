using ColorVision.Engine;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.BlackMura;
using ColorVision.Engine.Templates.LEDStripDetection;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI.PropertyEditor.Json;
using Newtonsoft.Json.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class TemplateEditorBindingTests
{
    [Fact]
    public void JsonTemplateSwitchReusesMatchingRowsWithoutCarryingPreviousEdits()
    {
        WpfTestHost.Invoke(() =>
        {
            JsonPropertyEditorControl editor = new();
            List<string> changes = [];
            editor.JsonValueChanged += (_, json) => changes.Add(json);

            static IEnumerable<Control> Fields(DependencyObject element)
            {
                if (element is Control control && control.Tag is string) yield return control;
                foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
                    foreach (var field in Fields(child)) yield return field;
            }

            var propertyPanel = (Panel)editor.FindName("PropertyPanel");
            Control Field(string path) => Assert.Single(Fields(propertyPanel), control => Equals(control.Tag, path));

            editor.SetJson("""{"Level": 1, "Options": {"Name": "first"}}""");
            Size size = new(800, 600);
            editor.Measure(size);
            editor.Arrange(new Rect(size));
            var level = Assert.IsType<TextBox>(Field("Level"));
            var name = Assert.IsType<TextBox>(Field("Options.Name"));
            level.SelectAll();
            level.SelectedText = "5";
            Assert.True(level.CanUndo);
            Assert.Single(changes);
            changes.Clear();

            editor.SetJson("""{"Level": 5, "Options": {"Name": "second"}}""");
            Assert.Same(level, Field("Level"));
            Assert.Same(name, Field("Options.Name"));
            Assert.Equal("5", level.Text);
            Assert.Equal("second", name.Text);
            Assert.False(level.CanUndo);
            Assert.False(name.CanUndo);
            Assert.Empty(changes);

            name.Text = "updated";
            var edited = JObject.Parse(Assert.Single(changes));
            Assert.Equal(5, edited.Value<int>("Level"));
            Assert.Equal("updated", (string?)edited.SelectToken("Options.Name"));
            Assert.True(JToken.DeepEquals(edited, JObject.Parse(editor.GetJson()!)));

            level.SelectAll();
            level.SelectedText = "9";
            Assert.True(level.Undo());
            Assert.False(level.CanUndo);
            Assert.True(level.CanRedo);
            changes.Clear();

            editor.SetJson("""{"Level": 5, "Options": {"Name": "after undo"}}""");
            Assert.Same(level, Field("Level"));
            Assert.False(level.CanRedo);
            Assert.Empty(changes);

            editor.SetJson("""{"Level": true, "Options": {"Name": "third"}}""");
            Assert.True(Assert.IsType<CheckBox>(Field("Level")).IsChecked);
            var newName = Assert.IsType<TextBox>(Field("Options.Name"));
            Assert.NotSame(name, newName);
            Assert.Equal("third", newName.Text);
            Assert.Empty(changes);
        });
    }

    [Fact]
    public void FilteredBatchActionsUseSelectedRowsInsteadOfVisibleIndicesOrSharedMarks()
    {
        WpfTestHost.Invoke(() =>
        {
            IConfigService previousConfig = ConfigService.Instance;
            List<ResourceDictionary> dictionaries = [];
            TemplateEditorWindow? window = null;
            OfflineBlackMuraTemplate template = new();
            try
            {
                TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false });
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    ResourceDictionary dictionary = new() { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }

                window = new TemplateEditorWindow(template) { Left = -10000, Top = -10000, ShowActivated = false };
                window.Show();
                PumpBindings();
                var list = (ListView)window.FindName("ListView1");
                var searchBar = (Grid)window.FindName("TemplateSearchBar");
                var search = (TextBox)window.FindName("Searchbox");
                Assert.Equal(Visibility.Collapsed, searchBar.Visibility);
                var nameColumn = Assert.Single(((GridView)list.View).Columns);
                Assert.Equal(ColorVision.Engine.Properties.Resources.Name, nameColumn.Header);
                Assert.Equal(SelectionMode.Extended, list.SelectionMode);

                var findCommand = window.CommandBindings.Cast<CommandBinding>().Select(binding => binding.Command)
                    .OfType<RoutedCommand>().Single(command => command.InputGestures.OfType<KeyGesture>().Any(gesture =>
                        gesture.Key == Key.F && gesture.Modifiers == ModifierKeys.Control));
                findCommand.Execute(null, list);
                Assert.Equal(Visibility.Visible, searchBar.Visibility);
                search.Text = "Match";
                PumpBindings();

                TemplateBase unfiltered = template.TemplateParams[0];
                TemplateBase first = template.TemplateParams[1];
                TemplateBase second = template.TemplateParams[2];
                Assert.Same(first, list.SelectedItem);
                var secondRow = Assert.IsType<ListViewItem>(list.ItemContainerGenerator.ContainerFromItem(second));
                secondRow.SetCurrentValue(ListViewItem.IsSelectedProperty, true);
                Assert.Equal(new[] { first, second }, list.SelectedItems.Cast<TemplateBase>());

                var export = Descendants(window).OfType<Button>().Single(button =>
                    Equals(button.ToolTip, ColorVision.Engine.Properties.Resources.Export));
                export.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(new[] { first, second }, template.Exported);

                var parameterArea = (Grid)window.FindName("GridProperty");
                Assert.False(ApplicationCommands.Delete.CanExecute(null, parameterArea));
                Assert.False(ApplicationCommands.Delete.CanExecute(null, list));
                var deleteShortcut = Assert.Single(list.InputBindings.OfType<KeyBinding>(), binding => binding.Key == Key.Delete);
                Assert.Equal(ModifierKeys.Alt, deleteShortcut.Modifiers);
                var deleteCommand = Assert.IsType<RoutedCommand>(deleteShortcut.Command);
                Assert.Empty(deleteCommand.InputGestures);

                string? prompt = null;
                window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    var dialog = window.OwnedWindows.OfType<MessageBoxWindow>().SingleOrDefault();
                    if (dialog == null) return;
                    prompt = ((TextBox)dialog.FindName("messageBoxText")).Text;
                    ((Button)dialog.FindName("ButtonOK")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }));
                deleteCommand.Execute(null, list);
                Assert.Equal(new[] { first, second }, template.Deleted);
                Assert.Contains(first.Key, prompt);
                Assert.Contains(second.Key, prompt);
                Assert.DoesNotContain(unfiltered.Key, prompt);
                Assert.Same(unfiltered, Assert.Single(template.TemplateParams));
                Assert.True(unfiltered.IsSelected);
            }
            finally
            {
                window?.Close();
                TemplateControl.ITemplateNames.Remove(template.Name);
                foreach (ResourceDictionary dictionary in dictionaries)
                    Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                TestEnvironment.SetConfig(previousConfig);
            }
        });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void ParameterEditorNeverInheritsWindowSettingsBeforeSelection(int defaultIndex)
    {
        WpfTestHost.Invoke(() =>
        {
            IConfigService previousConfig = ConfigService.Instance;
            List<ResourceDictionary> dictionaries = [];
            TemplateEditorWindow? window = null;
            try
            {
                TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false });
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    ResourceDictionary dictionary = new() { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }

                OfflineLedStripTemplate template = new();
                EditLEDStripDetection editor = template.EditLEDStripDetection;
                List<object?> contexts = [];
                editor.DataContextChanged += (_, e) => contexts.Add(e.NewValue);
                window = new TemplateEditorWindow(template, defaultIndex)
                {
                    Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false
                };
                window.Show();
                PumpBindings();

                Assert.Same(TemplateEditorWindow.Config, window.DataContext);
                // Capture synchronous inheritance too: a later SetParam can hide the initial failure.
                Assert.All(contexts.Where(context => context != null), context => Assert.IsType<LEDStripDetectionParam>(context));
                ListView list = (ListView)window.FindName("ListView1");
                if (defaultIndex < 0)
                    Assert.Null(editor.DataContext);
                list.SelectedIndex = 0;
                PumpBindings();

                LEDStripDetectionParam first = template.TemplateParams[0].Value;
                AssertParameterBindings(editor, first);
                TextBox method = Descendants(editor).OfType<TextBox>().Single(textBox =>
                    BindingOperations.GetBinding(textBox, TextBox.TextProperty)?.Path.Path == nameof(first.Method));
                ToggleButton debug = Descendants(editor).OfType<ToggleButton>().Single(toggle =>
                    BindingOperations.GetBinding(toggle, ToggleButton.IsCheckedProperty)?.Path.Path == nameof(first.IsDebug));
                method.Text = "7";
                method.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                debug.IsChecked = true;
                Assert.Equal(7, first.Method);
                Assert.True(first.IsDebug);

                list.SelectedIndex = 1;
                PumpBindings();
                LEDStripDetectionParam second = template.TemplateParams[1].Value;
                AssertParameterBindings(editor, second);
                Assert.Equal("2", method.Text);
                Assert.False(debug.IsChecked);
                second.Method = 3;
                PumpBindings();
                Assert.Equal("3", method.Text);
                Assert.Equal(7, first.Method);
                Assert.All(contexts.Where(context => context != null), context => Assert.IsType<LEDStripDetectionParam>(context));
            }
            finally
            {
                window?.Close();
                foreach (ResourceDictionary dictionary in dictionaries)
                    Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                TestEnvironment.SetConfig(previousConfig);
            }
        });
    }

    private static void AssertParameterBindings(EditLEDStripDetection editor, LEDStripDetectionParam param)
    {
        Assert.Same(param, editor.DataContext);
        List<BindingExpression> bindings = [];
        foreach (DependencyObject child in Descendants(editor))
        {
            DependencyProperty? property = child switch
            {
                TextBox => TextBox.TextProperty,
                ToggleButton => ToggleButton.IsCheckedProperty,
                Button => Button.CommandProperty,
                _ => null
            };
            if (property != null && BindingOperations.GetBindingExpression(child, property) is { } binding)
                bindings.Add(binding);
        }
        Assert.Equal(8, bindings.Count);
        Assert.All(bindings, binding =>
        {
            Assert.Equal(BindingStatus.Active, binding.Status);
            Assert.Same(param, binding.DataItem);
        });
        Button selectFile = Descendants(editor).OfType<Button>().Single(button =>
            BindingOperations.GetBinding(button, Button.CommandProperty)?.Path.Path == nameof(param.SelectFileCommand));
        Assert.Same(param.SelectFileCommand, selectFile.Command);
        Assert.NotNull(selectFile.Command);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void PumpBindings()
    {
        DispatcherFrame frame = new();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class OfflineLedStripTemplate : TemplateLEDStripDetection
    {
        public OfflineLedStripTemplate()
        {
            TemplateParams =
            [
                new("First", new LEDStripDetectionParam(new ModMasterModel { Id = -2 }, []) { Method = 1 }),
                new("Second", new LEDStripDetectionParam(new ModMasterModel { Id = -3 }, []) { Method = 2 })
            ];
        }

        public override void Load() { }
    }

    private sealed class OfflineBlackMuraTemplate : TemplateBlackMura
    {
        public OfflineBlackMuraTemplate()
        {
            TemplateParams =
            [
                new("Unfiltered", new TemplateJsonParam(new ModMasterModel { Id = 1, JsonVal = "{}" })) { IsSelected = true },
                new("Match first", new TemplateJsonParam(new ModMasterModel { Id = 2, JsonVal = "{}" })),
                new("Match second", new TemplateJsonParam(new ModMasterModel { Id = 3, JsonVal = "{}" }))
            ];
        }

        public IReadOnlyList<TemplateBase> Exported { get; private set; } = [];
        public IReadOnlyList<TemplateBase> Deleted { get; private set; } = [];
        public override void Load() { }

        internal override void ExportTemplates(IReadOnlyList<TemplateBase> targets) => Exported = targets;

        internal override void DeleteTemplates(IReadOnlyList<TemplateBase> targets)
        {
            Deleted = targets;
            foreach (var target in targets.Cast<TemplateModel<TemplateJsonParam>>()) TemplateParams.Remove(target);
        }
    }
}
