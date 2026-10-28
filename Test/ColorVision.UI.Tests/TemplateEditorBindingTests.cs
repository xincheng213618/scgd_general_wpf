using ColorVision.Engine;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.LEDStripDetection;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class TemplateEditorBindingTests
{
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
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
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
                ConfigService.SetInstance(previousConfig);
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
}
