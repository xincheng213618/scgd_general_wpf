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
using ColorVision.UI.Views;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class DeferredDeviceViewTests
{
    [Theory]
    [InlineData("Camera")]
    [InlineData("Algorithm")]
    [InlineData("Calibration")]
    [InlineData("Spectrum")]
    public void RegisteringTheSameShellPreservesItsIdentityWithoutCreatingContent(string kind)
    {
        Run(() =>
        {
            using ShellCase item = CreateShell(kind);
            // Use the actual registration path with an isolated manager, so callbacks left by
            // another test cannot open the shell or affect existing documents.
            FieldInfo instanceField = typeof(DockViewManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
            object? previousManager = instanceField.GetValue(null);
            var manager = (DockViewManager)Activator.CreateInstance(typeof(DockViewManager), nonPublic: true)!;
            instanceField.SetValue(null, manager);
            try
            {
                var card = new UserControl();
                card.AddViewConfig(item.GetShell(), kind);
                card.AddViewConfig(item.GetShell(), kind);

                Assert.Same(item.View, Assert.Single(manager.Views));
                Assert.Equal(kind, manager.ViewTitles[item.View]);
                Assert.Empty(item.View.CommandBindings);
                Assert.Null(item.View.Content);
                Assert.Null(item.View.DataContext);
                Assert.Null(item.View.FindName("listView1"));
            }
            finally
            {
                instanceField.SetValue(null, previousManager);
            }
        }, loadResources: false);
    }

    [Theory]
    [InlineData("Camera")]
    [InlineData("Algorithm")]
    [InlineData("Calibration")]
    [InlineData("Spectrum")]
    public void HiddenLoadedShellInitializesWhenVisibleAndReusesItsContent(string kind)
    {
        Run(() =>
        {
            using ShellCase item = CreateShell(kind);
            var container = new Grid { Visibility = Visibility.Hidden };
            container.Children.Add(item.View);
            var window = CreateWindow(container);
            try
            {
                window.Show();
                PumpDispatcher();

                Assert.True(item.View.IsInitialized);
                Assert.True(item.View.IsLoaded);
                Assert.False(item.View.IsVisible);
                Assert.Null(item.View.Content);

                container.Visibility = Visibility.Visible;
                PumpDispatcher();
                Assert.True(item.View.IsVisible);
                AssertReady(item.View);
                object content = item.View.Content;
                var resultList = Assert.IsType<ListView>(item.View.FindName("listView1"));
                int commandCount = resultList.CommandBindings.Count;

                container.Visibility = Visibility.Hidden;
                PumpDispatcher();
                container.Visibility = Visibility.Visible;
                PumpDispatcher();
                item.EnsureInitialized();

                Assert.Same(item.View, item.GetPublicView());
                Assert.Same(content, item.View.Content);
                Assert.Same(resultList, item.View.FindName("listView1"));
                Assert.Equal(commandCount, resultList.CommandBindings.Count);
            }
            finally
            {
                window.Close();
                PumpDispatcher();
            }
        });
    }

    [Theory]
    [InlineData("Camera")]
    [InlineData("Algorithm")]
    [InlineData("Calibration")]
    [InlineData("Spectrum")]
    public void PublicViewInitializesAnAlreadyInitializedUnloadedShell(string kind)
    {
        Run(() =>
        {
            using ShellCase item = CreateShell(kind);
            item.View.BeginInit();
            item.View.EndInit();
            Assert.True(item.View.IsInitialized);
            Assert.False(item.View.IsLoaded);
            Assert.Null(item.View.Content);

            Assert.Same(item.View, item.GetPublicView());
            AssertReady(item.View);
            object content = item.View.Content;
            Assert.Same(item.View, item.GetPublicView());
            Assert.Same(content, item.View.Content);
        });
    }

    [Theory]
    [InlineData("Camera")]
    [InlineData("Algorithm")]
    [InlineData("Calibration")]
    public void DisposingUnopenedShellUnsubscribesAndPreventsLaterInitialization(string kind)
    {
        Run(() =>
        {
            using ShellCase item = CreateShell(kind);
            Assert.NotNull(item.Service!.MsgReturnReceived);
            item.Dispose();
            item.Dispose();
            Assert.Null(item.Service.MsgReturnReceived);

            item.EnsureInitialized();
            var window = CreateWindow(item.View);
            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(item.View.IsLoaded);
                Assert.True(item.View.IsVisible);
                Assert.Null(item.View.Content);
                Assert.Null(item.View.DataContext);
            }
            finally
            {
                window.Close();
                PumpDispatcher();
            }
        }, loadResources: false);
    }

    [Theory]
    [InlineData("Camera")]
    [InlineData("Calibration")]
    public void FirstImageResultInitializesUnopenedShellAndKeepsTheResult(string kind)
    {
        Run(() =>
        {
            using ShellCase item = CreateShell(kind);
            var model = new MeasureResultImgModel { Id = 731, FileUrl = string.Empty };
            Assert.Null(item.View.Content);
            // Enter after DAO lookup with an in-memory result. Empty image paths avoid file
            // decoding, and auto-refresh is disabled so no result handler can query storage.
            if (item.View is ViewCamera camera)
            {
                ViewCamera.Config.AutoRefreshView = false;
                camera.ShowResult(model);
                Assert.Equal(model.Id, Assert.Single(camera.ViewResults).Id);
            }
            else
            {
                var calibration = Assert.IsType<ViewCalibration>(item.View);
                ViewCalibration.Config.AutoRefreshView = false;
                calibration.ShowResult(model);
                Assert.Equal(model.Id, Assert.Single(calibration.ViewResults).Id);
            }

            AssertReady(item.View);
            Assert.False(item.View.IsLoaded);
            Assert.Same(item.View, item.GetPublicView());
            Assert.Single(Assert.IsType<ListView>(item.View.FindName("listView1")).Items);
        });
    }

    private static void AssertReady(UserControl view)
    {
        Assert.NotNull(view.Content);
        Assert.NotNull(view.DataContext);
        var results = Assert.IsType<ListView>(view.FindName("listView1"));
        Assert.NotNull(results.ItemsSource);
        Assert.True(results.CommandBindings.Count >= 3);
        if (view is AlgorithmView algorithm)
        {
            Assert.NotNull(algorithm.ImageView);
            Assert.NotNull(algorithm.ViewResultContext);
            Assert.Same(algorithm.ImageView, algorithm.ViewResultContext.ImageView);
            Assert.Same(algorithm.ListView, algorithm.ViewResultContext.ListView);
        }
    }

    private static ShellCase CreateShell(string kind)
    {
        // Constructors of real devices/MQTT services discover hardware, subscribe to the
        // broker, and/or read the database. Supply only the state used by the real view.
        switch (kind)
        {
            case "Camera":
            {
                var device = Uninitialized<DeviceCamera>();
                device.Config = new() { Code = "deferred-test-camera" };
                device.DService = Uninitialized<MQTTCamera>();
                var view = new ViewCamera(device, deferInitialization: true);
                SetView(device, view);
                return new(view, () => device.ViewShell, () => device.View, view.EnsureInitialized, device.DService);
            }
            case "Algorithm":
            {
                var device = Uninitialized<DeviceAlgorithm>();
                device.Config = new() { Code = "deferred-test-algorithm" };
                device.DService = Uninitialized<MQTTAlgorithm>();
                var view = new AlgorithmView(device, deferInitialization: true);
                SetView(device, view);
                return new(view, () => device.ViewShell, () => device.View, view.EnsureInitialized, device.DService);
            }
            case "Calibration":
            {
                var device = Uninitialized<DeviceCalibration>();
                device.Config = new() { Code = "deferred-test-calibration" };
                device.DService = Uninitialized<MQTTCalibration>();
                var view = new ViewCalibration(device, deferInitialization: true);
                SetView(device, view);
                return new(view, () => device.ViewShell, () => device.View, view.EnsureInitialized, device.DService);
            }
            case "Spectrum":
            {
                var device = Uninitialized<DeviceSpectrum>();
                device.Config = new() { Code = "deferred-test-spectrum" };
                var view = new ViewSpectrum(device, deferInitialization: true);
                SetView(device, view);
                return new(view, () => device.ViewShell, () => device.View, view.EnsureInitialized, null);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static T Uninitialized<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private static void SetView<T>(object device, T view) where T : UserControl
    {
        FieldInfo field = device.GetType().GetField("_view", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(device, new Lazy<T>(() => view));
    }

    private sealed record ShellCase(UserControl View, Func<UserControl> GetShell,
        Func<UserControl> GetPublicView, Action EnsureInitialized, MQTTServiceBase? Service) : IDisposable
    {
        public void Dispose() => (View as IDisposable)?.Dispose();
    }

    private static Window CreateWindow(UIElement content) => new()
    {
        Content = content,
        Width = 900,
        Height = 620,
        Left = -10000,
        Top = -10000,
        ShowActivated = false,
        ShowInTaskbar = false,
        WindowStartupLocation = WindowStartupLocation.Manual,
    };

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void Run(Action action, bool loadResources = true) => WpfTestHost.Invoke(() =>
    {
        IConfigService? previousConfig = ConfigService.Instance;
        ResourceDictionary previousResources = Application.Current.Resources;
        Window? previousMainWindow = Application.Current.MainWindow;
        try
        {
            ConfigService.SetInstance(new MemoryOnlyConfigService());
            Application.Current.Resources = loadResources ? new ThemeResourceDictionary() : new ResourceDictionary();
            action();
        }
        finally
        {
            Application.Current.MainWindow = previousMainWindow;
            Application.Current.Resources = previousResources;
            ConfigService.SetInstance(previousConfig!);
        }
    });

    private sealed class MemoryOnlyConfigService : IConfigService
    {
        private readonly ConfigHandler defaults = new();
        public IConfig GetRequiredService(Type type) => defaults.GetRequiredService(type);
        public T GetRequiredService<T>() where T : IConfig => defaults.GetRequiredService<T>();
        public void LoadConfigs() => throw new InvalidOperationException("Deferred view tests must not read persisted configuration.");
        public void SaveConfigs() => throw new InvalidOperationException("Deferred view tests must not persist configuration.");
        public void Save<T>() where T : IConfig => throw new InvalidOperationException("Deferred view tests must not persist configuration.");
    }
}
