using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Calibration.Views;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Themes;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class DeferredDeviceViewBindingTests
{
    [Theory]
    [InlineData("camera")]
    [InlineData("spectrum")]
    [InlineData("algorithm")]
    [InlineData("calibration")]
    public void DeferredViewUsesItsOwnConfigurationFromFirstBindingEvaluation(string kind)
    {
        WpfTestHost.Invoke(() =>
        {
            IConfigService previousConfig = ConfigService.Instance;
            List<ResourceDictionary> dictionaries = [];
            Window? window = null;
            try
            {
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    ResourceDictionary dictionary = new() { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }

                SysResourceModel resource = new() { Id = -2, Code = $"binding-{kind}-{Guid.NewGuid():N}", Name = "Offline binding test", Value = "{}" };
                using DeviceService device = kind switch
                {
                    "camera" => new DeviceCamera(resource),
                    "spectrum" => new DeviceSpectrum(resource),
                    "algorithm" => new DeviceAlgorithm(resource),
                    "calibration" => new DeviceCalibration(resource),
                    _ => throw new ArgumentException(kind)
                };
                UserControl view = device switch
                {
                    DeviceCamera camera => new ViewCamera(camera, true),
                    DeviceSpectrum spectrum => new ViewSpectrum(spectrum, true),
                    DeviceAlgorithm algorithm => new AlgorithmView(algorithm, true),
                    DeviceCalibration calibration => new ViewCalibration(calibration, true),
                    _ => throw new ArgumentException(kind)
                };
                using IDisposable lifetime = (IDisposable)view;
                Grid host = new();
                window = new Window
                {
                    Content = host, DataContext = new MainWindowConfig(), Width = 800, Height = 600,
                    Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false
                };
                window.Show();
                using BindingErrors errors = new();
                host.Children.Add(view);
                // Dock registration initializes the empty shell before loading its XAML.
                Assert.True(view.IsInitialized);
                Assert.Null(view.Content);

                PumpBindings();
                Assert.NotNull(view.Content);
                Assert.Empty(errors.Messages);

                ViewConfigBase config = kind switch
                {
                    "camera" => ViewCamera.Config,
                    "spectrum" => ViewSpectrum.Config,
                    "algorithm" => ((AlgorithmView)view).Config,
                    "calibration" => ViewCalibration.Config,
                    _ => throw new ArgumentException(kind)
                };
                Assert.Same(kind == "spectrum" ? view : config, view.DataContext);
                ListView list = Assert.IsType<ListView>(view.FindName("listView1"));
                BindingExpression height = Assert.IsType<BindingExpression>(BindingOperations.GetBindingExpression(list, FrameworkElement.HeightProperty));
                Assert.Equal(BindingStatus.Active, height.Status);
                Assert.NotNull(list.ItemsSource);

                if (kind is "camera" or "algorithm")
                    AssertResultMenuOpensLazily(view, list);

                Button settings = Descendants(view).OfType<Button>().Single(button => ReferenceEquals(button.Command, config.EditCommand));
                Assert.NotNull(settings.Command);
                if (kind != "spectrum")
                {
                    ToggleButton toggle = Descendants(view).OfType<ToggleButton>().Single(button =>
                        BindingOperations.GetBinding(button, ToggleButton.IsCheckedProperty)?.Path.Path == "IsShowListView");
                    toggle.IsChecked = false;
                    PumpBindings();
                    Assert.Equal(Visibility.Collapsed, list.Visibility);
                    toggle.IsChecked = true;
                    PumpBindings();
                    Assert.Equal(Visibility.Visible, list.Visibility);
                }

                window.DataContext = new MainWindowConfig();
                PumpBindings();
                Assert.Empty(errors.Messages);
                Assert.Same(config.EditCommand, settings.Command);
                host.Children.Clear();
            }
            finally
            {
                if (window != null) { window.Content = null; window.Close(); }
                foreach (ResourceDictionary dictionary in dictionaries)
                    Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                ConfigService.SetInstance(previousConfig);
            }
        });
    }

    private static void AssertResultMenuOpensLazily(UserControl view, ListView list)
    {
        object result;
        if (view is ViewCamera camera)
        {
            ViewResultImage image = new(new MeasureResultImgModel { Id = 1, FileUrl = string.Empty });
            camera.ViewResults.Add(image);
            result = image;
        }
        else
        {
            ViewResultAlg algorithm = new() { Id = 1 };
            ((AlgorithmView)view).ViewResults.Add(algorithm);
            result = algorithm;
        }
        PumpBindings();
        list.UpdateLayout();
        ListViewItem container = Assert.IsType<ListViewItem>(list.ItemContainerGenerator.ContainerFromIndex(0));
        Assert.Null(result.GetType().GetField("_contextMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(result));
        // WPF exposes no public constructor for this routed input event.
        ContextMenuEventArgs opening = (ContextMenuEventArgs)Activator.CreateInstance(typeof(ContextMenuEventArgs),
            BindingFlags.Instance | BindingFlags.NonPublic, null, [container, true, 0d, 0d], null)!;
        opening.RoutedEvent = ContextMenuService.ContextMenuOpeningEvent;
        container.RaiseEvent(opening);
        ContextMenu menu = (ContextMenu)result.GetType().GetProperty("ContextMenu")!.GetValue(result)!;
        Assert.True(opening.Handled);
        Assert.True(menu.IsOpen);
        Assert.Same(container, menu.PlacementTarget);
        Assert.NotEmpty(menu.Items);
        menu.IsOpen = false;
        // Drive the Closed boundary directly, independent of popup animation scheduling.
        menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent, menu));
        PumpBindings();
        Assert.Null(menu.PlacementTarget);
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

    private sealed class BindingErrors : TraceListener
    {
        private readonly SourceLevels previousLevel = PresentationTraceSources.DataBindingSource.Switch.Level;
        public List<string> Messages { get; } = [];

        public BindingErrors()
        {
            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            PresentationTraceSources.DataBindingSource.Listeners.Add(this);
        }

        public override void Write(string? message) { if (message != null) Messages.Add(message); }
        public override void WriteLine(string? message) => Write(message);

        protected override void Dispose(bool disposing)
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(this);
            PresentationTraceSources.DataBindingSource.Switch.Level = previousLevel;
            base.Dispose(disposing);
        }
    }
}
