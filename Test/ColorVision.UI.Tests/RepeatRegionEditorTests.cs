using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Themes;
using FlowEngineLib;
using FlowEngineLib.Base;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class RepeatRegionEditorTests
{
    // The editor is a detached transaction: resizing, generating and cancelling must preserve saved values.
    [Fact]
    public void WorkingArraysResizeTogetherAndOnlyApplyCommitsThem()
    {
        StaTest.Run(() =>
        {
            var camera = new LocalCameraNode { ExpTime = 350, AvgCount = 2 };
            var spectrum = new LocalSpectrumNode { IntegralTime = 250, NumberOfAverage = 3 };
            camera.Create(); spectrum.Create();
            var session = new RepeatRegionEditSession([camera, spectrum], 3);
            var exposure = session.Nodes[0].Parameters.Single(parameter => parameter.Definition.Key == nameof(camera.ExpTime));
            Assert.Equal(new double[] { 350, 350, 350 }, exposure.Values);
            exposure.Fill(0, 2, 100, 50);
            session.Resize(5);
            Assert.Equal(new double[] { 100, 150, 200, 350, 350 }, exposure.Values);
            session.RemoveRounds([1, 3]);
            Assert.Equal(new double[] { 100, 200, 350 }, exposure.Values);
            Assert.All(session.Nodes.SelectMany(node => node.Parameters), parameter => Assert.Equal(3, parameter.Values.Length));
            Assert.Empty(camera.RepeatParameterValues);
            Assert.Empty(spectrum.RepeatParameterValues);
            session.Apply(Guid.NewGuid().ToString(), [camera, spectrum]);
            Assert.Equal(3, camera.RepeatCount);
            Assert.Equal(3, spectrum.RepeatCount);
            Assert.Equal(new double[] { 100, 200, 350 }, camera.RepeatParameterValues[nameof(camera.ExpTime)]);
            Assert.Equal(new double[] { 250, 250, 250 }, spectrum.RepeatParameterValues[nameof(spectrum.IntegralTime)]);
            Assert.Equal(350, camera.ExpTime);
            Assert.Equal(250, spectrum.IntegralTime);
        });
    }

    [Fact]
    public void InvalidBulkFillIsAtomicAndAutomaticExposureKeepsAverageEditable()
    {
        StaTest.Run(() =>
        {
            var camera = new LocalCameraNode { IsAutoExp = true, AvgCount = 2 };
            camera.Create();
            var session = new RepeatRegionEditSession([camera], 3);
            var exposure = session.Nodes[0].Parameters.Single(parameter => parameter.Definition.Key == nameof(camera.ExpTime));
            var average = session.Nodes[0].Parameters.Single(parameter => parameter.Definition.Key == nameof(camera.AvgCount));
            Assert.False(exposure.Enabled);
            Assert.True(average.Enabled);
            Assert.Throws<InvalidOperationException>(() => average.Fill(0, 2, 2, -1));
            Assert.Equal(new double[] { 2, 2, 2 }, average.Values);
            Assert.Throws<InvalidOperationException>(() => session.RemoveRounds([0, 1, 2]));
            Assert.Equal(3, session.Count);
            Assert.Empty(camera.RepeatParameterValues);
            var smu = new LocalSmuNode();
            var limit = smu.GetRepeatParameters().Single(parameter => parameter.Key == nameof(smu.LimitValue));
            Assert.False(limit.TryValidate(0, out _));
        });
    }

    // Exercise actual WPF events: parameter toggles and repair operations must not discard cell drafts.
    [Fact]
    public void TableDraftsSurviveTogglesAndCanBeRepairedByFillDeleteAndResize()
    {
        WithTheme(() =>
        {
            var camera = new LocalCameraNode { ExpTime = 350, AvgCount = 2 };
            camera.Create();
            var window = new FlowRepeatRegionWindow([camera], 3) { Left = -16000, Top = -16000, ShowActivated = false };
            try
            {
                window.Show(); window.UpdateLayout();
                var grid = (DataGrid)window.FindName("ParameterGrid");
                var view = Assert.IsType<DataView>(grid.ItemsSource);
                view[0][nameof(camera.ExpTime)] = "123";
                view[1][nameof(camera.AvgCount)] = "bad";
                var options = (WrapPanel)window.FindName("ParameterOptions");
                CheckBox exposure = options.Children.OfType<CheckBox>().First();
                exposure.IsChecked = false; exposure.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                exposure.IsChecked = true; exposure.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Equal("123", view[0][nameof(camera.ExpTime)]);
                Assert.Equal("bad", view[1][nameof(camera.AvgCount)]);
                Assert.False(window.ValidateEdits());
                var selector = (ComboBox)window.FindName("ParameterSelector");
                selector.SelectedItem = selector.Items.Cast<RepeatParameterEditSeries>().Single(parameter => parameter.Definition.Key == nameof(camera.AvgCount));
                SetText(window, "StartValueBox", "3"); SetText(window, "IncrementBox", "0");
                Click(window, "FillButton");
                Assert.True(window.ValidateEdits());
                Assert.Equal(123, window.Session.Nodes[0].Parameters[0].Values[0]);
                Assert.All(window.Session.Nodes[0].Parameters[1].Values, value => Assert.Equal(3, value));

                view[1][nameof(camera.ExpTime)] = "invalid";
                grid.SelectedItem = view[1];
                Click(window, "RemoveRoundsButton");
                Assert.Equal(2, window.Session.Count);
                Assert.True(window.ValidateEdits());
                view = Assert.IsType<DataView>(grid.ItemsSource);
                view[1][nameof(camera.ExpTime)] = "invalid tail";
                SetText(window, "RepeatCountBox", "1"); Click(window, "ApplyCountButton");
                Assert.Equal(1, window.Session.Count);
                Assert.True(window.ValidateEdits());
                Click(window, "AddRoundButton");
                Assert.Equal(2, window.Session.Count);
                Assert.Equal(350, window.Session.Nodes[0].Parameters[0].Values[1]);
                Assert.Empty(camera.RepeatParameterValues);
                Assert.Equal(350, camera.ExpTime);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SupportedDeviceEditorRendersWithRoundArraysAndKeepsCancellationIsolated(bool currentSource)
    {
        WithTheme(() =>
        {
            var camera = new LocalCameraNode { ExpTime = 350, AvgCount = 2 };
            var smu = new LocalSmuNode();
            var spectrum = new LocalSpectrumNode();
            CVCommonNode[] nodes = [camera, smu, spectrum];
            foreach (var node in nodes) node.Create();
            var valueProperty = typeof(LocalSmuNode).GetProperty(nameof(smu.MeasureValue))!;
            var limitProperty = typeof(LocalSmuNode).GetProperty(nameof(smu.LimitValue))!;
            var valueEditor = (IPropertyEditor)Activator.CreateInstance(valueProperty.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType!)!;
            var limitEditor = (IPropertyEditor)Activator.CreateInstance(limitProperty.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType!)!;
            var valuePanel = valueEditor.GenProperties(valueProperty, smu);
            var limitPanel = limitEditor.GenProperties(limitProperty, smu);
            var valueLabel = Assert.Single(valuePanel.Children.OfType<TextBlock>());
            var limitLabel = Assert.Single(limitPanel.Children.OfType<TextBlock>());
            Assert.Equal("设定电压 (V)", valueLabel.Text);
            Assert.Equal("电流上限 (mA)", limitLabel.Text);
            smu.Source = SourceType.Current_I;
            Assert.Equal("设定电流 (mA)", valueLabel.Text);
            Assert.Equal("电压上限 (V)", limitLabel.Text);
            if (!currentSource) smu.Source = SourceType.Voltage_V;
            string expectedValueLabel = currentSource ? "设定电流 (mA)" : "设定电压 (V)";
            string expectedLimitLabel = currentSource ? "电压上限 (V)" : "电流上限 (mA)";
            Assert.Equal(expectedValueLabel, valueLabel.Text);
            Assert.Equal(expectedLimitLabel, limitLabel.Text);
            camera.SetRepeatParameterValues(new Dictionary<string, double[]>
            {
                [nameof(camera.ExpTime)] = Enumerable.Range(0, 10).Select(index => 100d + index * 25).ToArray(),
                [nameof(camera.AvgCount)] = Enumerable.Repeat(2d, 10).ToArray()
            });
            var window = new FlowRepeatRegionWindow(nodes, 10) { Left = -16000, Top = -16000, ShowActivated = false };
            try
            {
                window.Show(); window.UpdateLayout();
                Assert.Equal(3, ((ListBox)window.FindName("NodeList")).Items.Count);
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                window.UpdateLayout();
                var parameterGrid = (DataGrid)window.FindName("ParameterGrid");
                Assert.All(parameterGrid.Columns, column => Assert.True(column.ActualWidth > 0, $"Column {column.Header} has width {column.ActualWidth}."));
                Assert.True(window.ValidateEdits());
                SaveImage(window, "repeat-parameters.png");
                window.Width = 880; window.Height = 590; window.UpdateLayout();
                SaveImage(window, "repeat-parameters-minimum.png");
                ((ListBox)window.FindName("NodeList")).SelectedIndex = 1;
                Assert.Equal(nameof(LocalSmuNode.MeasureValue), window.Session.Nodes[1].Parameters[0].Definition.Key);
                Assert.Equal(new[] { "轮次", expectedValueLabel, expectedLimitLabel }, parameterGrid.Columns.Select(column => column.Header));
                Assert.Equal(new[] { expectedValueLabel, expectedLimitLabel },
                    ((WrapPanel)window.FindName("ParameterOptions")).Children.OfType<CheckBox>().Select(option => option.Content));
                Assert.Equal(new[] { expectedValueLabel, expectedLimitLabel },
                    ((ComboBox)window.FindName("ParameterSelector")).Items.Cast<RepeatParameterEditSeries>().Select(parameter => parameter.ToString()));
                SaveImage(window, currentSource ? "repeat-parameters-smu-current.png" : "repeat-parameters-smu.png");
                ((ListBox)window.FindName("NodeList")).SelectedIndex = 2;
                Assert.Equal(nameof(LocalSpectrumNode.IntegralTime), window.Session.Nodes[2].Parameters[0].Definition.Key);
                SaveImage(window, "repeat-parameters-spectrum.png");
                foreach (string uri in ThemeManager.ResourceDictionaryDark)
                    window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) });
                ((ListBox)window.FindName("NodeList")).SelectedIndex = 0;
                window.Width = 1060; window.Height = 720;
                SaveImage(window, "repeat-parameters-dark.png");
            }
            finally { window.Close(); }
            Assert.Empty(smu.RepeatParameterValues);
            Assert.Empty(spectrum.RepeatParameterValues);
            Assert.Equal(100, camera.RepeatParameterValues[nameof(camera.ExpTime)][0]);
        });
    }

    private static void WithTheme(Action action) => WpfTestHost.Invoke(() =>
    {
        var dictionaries = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
            .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) }).ToArray();
        foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Add(dictionary);
        try { action(); }
        finally { foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary); }
    });

    private static void SetText(Window window, string name, string value) => ((TextBox)window.FindName(name)).Text = value;
    private static void Click(Window window, string name) => ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static void SaveImage(Window window, string name)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        window.UpdateLayout();
        var content = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
        var image = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        image.Render(content);
        string directory = Path.Combine(AppContext.BaseDirectory, "editor-evidence");
        Directory.CreateDirectory(directory);
        using var file = File.Create(Path.Combine(directory, name));
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); encoder.Save(file);
    }
}
