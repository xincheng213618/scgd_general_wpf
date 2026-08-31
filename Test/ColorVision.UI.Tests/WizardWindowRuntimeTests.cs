using ColorVision.UI.Desktop.Wizards;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class WizardWindowRuntimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RuntimeWizardStillDiscoversRefreshesAppliesAndFinishesWithoutRunningInitializers(
        bool firstRunsBeforeInitializers, bool secondRunsBeforeInitializers)
    {
        WithWizard(false, firstRunsBeforeInitializers, secondRunsBeforeInitializers, false, (window, config, configPath, owner) =>
        {
            Assert.Equal(2, WizardManager.GetInstance().IWizardSteps.Count);
            Assert.Single(WizardManager.GetInstance().WizardInitializers);
            Assert.Equal(1, FirstStep.RefreshCount);
            Assert.Equal(0, RecordingInitializer.RunCount);
            Assert.False(config.WizardCompletionKey);
            Assert.False(File.Exists(configPath));

            Click(window, "BtnNext");
            Assert.Equal(1, FirstStep.ApplyCount);
            Assert.Equal(1, SecondStep.RefreshCount);
            Assert.Equal(1, Assert.IsType<ListBox>(window.FindName("ListWizard")).SelectedIndex);
            Assert.Equal(0, RecordingInitializer.RunCount);

            bool closed = false;
            window.Closed += (_, _) => closed = true;
            Click(window, "BtnFinish");
            Assert.Equal(1, SecondStep.ApplyCount);
            Assert.Equal(0, RecordingInitializer.RunCount);
            Assert.True(config.WizardCompletionKey);
            Assert.True(closed);
            Assert.Same(owner, Application.Current.MainWindow);
            Assert.False(Application.Current.Dispatcher.HasShutdownStarted);
            Assert.True(JObject.Parse(File.ReadAllText(configPath))[nameof(WizardWindowConfig)]![nameof(WizardWindowConfig.WizardCompletionKey)]!.Value<bool>());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpeningAndClosingRuntimeWizardDoesNotChangeCompletionOrSaveConfiguration(bool initiallyComplete)
    {
        WithWizard(false, false, false, initiallyComplete, (window, config, configPath, _) =>
        {
            Assert.Equal(initiallyComplete, config.WizardCompletionKey);
            Assert.Equal(1, FirstStep.RefreshCount);
            Assert.Equal(0, FirstStep.ApplyCount);
            window.Close();
            Assert.Equal(initiallyComplete, config.WizardCompletionKey);
            Assert.Equal(0, RecordingInitializer.RunCount);
            Assert.False(File.Exists(configPath));
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void OriginalParameterlessConstructorKeepsStartupInitializerTiming(bool firstBefore, bool secondBefore)
    {
        WithWizard(null, firstBefore, secondBefore, false, (window, _, _, _) =>
        {
            Assert.Equal(firstBefore || secondBefore ? 0 : 1, RecordingInitializer.RunCount);
            Click(window, "BtnNext");
            Assert.Equal(secondBefore ? 0 : 1, RecordingInitializer.RunCount);
            Click(window, "BtnFinish");
            Assert.Equal(1, RecordingInitializer.RunCount);
            Assert.True(RecordingInitializer.WasFirstRun);
            Assert.Same(window, RecordingInitializer.Owner);
        });
    }

    [Fact]
    public void ExplicitStartupOptionRunsInitializersAndRuntimeApplyFailureDoesNotAdvance()
    {
        WithWizard(true, false, false, true, (_, _, _, _) =>
        {
            Assert.Equal(1, RecordingInitializer.RunCount);
            Assert.False(RecordingInitializer.WasFirstRun);
        });
        WithWizard(false, true, false, true, (window, _, configPath, _) =>
        {
            FirstStep.ApplySucceeds = false;
            Click(window, "BtnNext");
            Assert.Equal(1, FirstStep.ApplyCount);
            Assert.Equal(0, Assert.IsType<ListBox>(window.FindName("ListWizard")).SelectedIndex);
            Assert.Equal(0, SecondStep.RefreshCount);
            Assert.Equal(0, RecordingInitializer.RunCount);
            Assert.False(File.Exists(configPath));
        });
    }

    private static void Click(WizardWindow window, string name)
    {
        Button button = Assert.IsType<Button>(window.FindName(name));
        Assert.True(button.IsEnabled);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        Drain(window);
    }

    private static void Drain(WizardWindow window)
        => window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void WithWizard(bool? runInitializers, bool firstBefore, bool secondBefore, bool initiallyComplete,
        Action<WizardWindow, WizardWindowConfig, string, Window> inspect)
    {
        WpfTestHost.Invoke(() =>
        {
            // Only these three fake types are discoverable. No production wizard step, service, device,
            // App constructor, persisted config loader, or process launcher is used by this fixture.
            FieldInfo assembliesField = typeof(AssemblyHandler).GetField("_assemblies", BindingFlags.Instance | BindingFlags.NonPublic)!;
            FieldInfo configInstanceField = typeof(ConfigHandler).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
            FieldInfo managerInstanceField = typeof(WizardManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
            AssemblyHandler assemblyHandler = AssemblyHandler.GetInstance();
            object? previousAssemblies = assembliesField.GetValue(assemblyHandler);
            object? previousConfigHandler = configInstanceField.GetValue(null);
            object? previousWizardManager = managerInstanceField.GetValue(null);
            IConfigService? previousConfigService = ConfigService.Instance;
            Application app = Application.Current;
            Window? previousMainWindow = app.MainWindow;
            var previousResources = new[] { "ButtonDefault", "ButtonPrimary" }
                .ToDictionary(key => key, key => app.Resources.Keys.Cast<object>().Contains(key) ? app.Resources[key] : null);
            string root = Directory.CreateTempSubdirectory("ColorVisionWizardTests-").FullName;
            string configPath = Path.Combine(root, "WizardConfig.json");
            var config = new WizardWindowConfig { IsRestoreWindow = false, WizardCompletionKey = initiallyComplete };
            var isolatedConfig = new ConfigHandler { ConfigFilePath = configPath, IsAutoSave = false };
            isolatedConfig.Configs[typeof(WizardWindowConfig)] = config;
            Window? owner = null;
            WizardWindow? window = null;
            bool windowClosed = false;
            try
            {
                FirstStep.Reset(firstBefore);
                SecondStep.Reset(secondBefore);
                RecordingInitializer.Reset();
                assembliesField.SetValue(assemblyHandler, new Assembly[] { new FakeWizardAssembly() });
                managerInstanceField.SetValue(null, new WizardManager());
                configInstanceField.SetValue(null, isolatedConfig);
                ConfigService.SetInstance(isolatedConfig);
                app.Resources["ButtonDefault"] = new Style(typeof(Button));
                app.Resources["ButtonPrimary"] = new Style(typeof(Button));
                owner = new Window();
                app.MainWindow = owner;
                window = runInitializers.HasValue ? new WizardWindow(runInitializers.Value) : new WizardWindow();
                window.Left = -10000;
                window.Top = -10000;
                window.Closed += (_, _) => windowClosed = true;
                Drain(window);
                inspect(window, config, configPath, owner);
            }
            finally
            {
                if (window != null && !windowClosed) window.Close();
                owner?.Close();
                app.MainWindow = previousMainWindow;
                assembliesField.SetValue(assemblyHandler, previousAssemblies);
                configInstanceField.SetValue(null, previousConfigHandler);
                managerInstanceField.SetValue(null, previousWizardManager);
                ConfigService.SetInstance(previousConfigService!);
                RecordingInitializer.Reset();
                foreach ((string key, object? value) in previousResources)
                {
                    if (value == null) app.Resources.Remove(key);
                    else app.Resources[key] = value;
                }
                if (File.Exists(configPath)) File.Delete(configPath);
                Directory.Delete(root); // Only this newly created, now-empty fixture directory; never recursive.
            }
        });
    }

    private sealed class FakeWizardAssembly : Assembly
    {
        public override Type[] GetTypes() => [typeof(SecondStep), typeof(RecordingInitializer), typeof(FirstStep)];
    }

    public sealed class FirstStep : WizardStepBase
    {
        internal static bool BeforeInitializers;
        internal static bool ApplySucceeds;
        internal static int RefreshCount;
        internal static int ApplyCount;
        public override string Header => "First test step";
        public override int Order => 1;
        public override string Description => "Only records refresh and apply.";
        public override bool RunsBeforeInitializers => BeforeInitializers;
        public override Task RefreshAsync(CancellationToken cancellationToken = default) { RefreshCount++; return Task.CompletedTask; }
        public override Task<bool> ApplyAsync(CancellationToken cancellationToken = default) { ApplyCount++; return Task.FromResult(ApplySucceeds); }
        internal static void Reset(bool before) { BeforeInitializers = before; ApplySucceeds = true; RefreshCount = ApplyCount = 0; }
    }

    public sealed class SecondStep : WizardStepBase
    {
        internal static bool BeforeInitializers;
        internal static int RefreshCount;
        internal static int ApplyCount;
        public override string Header => "Second test step";
        public override int Order => 2;
        public override string Description => "Does not install or configure anything.";
        public override bool RunsBeforeInitializers => BeforeInitializers;
        public override Task RefreshAsync(CancellationToken cancellationToken = default) { RefreshCount++; return Task.CompletedTask; }
        public override Task<bool> ApplyAsync(CancellationToken cancellationToken = default) { ApplyCount++; return Task.FromResult(true); }
        internal static void Reset(bool before) { BeforeInitializers = before; RefreshCount = ApplyCount = 0; }
    }

    public sealed class RecordingInitializer : IWizardInitializer
    {
        internal static int RunCount;
        internal static bool WasFirstRun;
        internal static Window? Owner;
        public int Order => 1;
        public void Initialize(WizardInitializationContext context) { RunCount++; WasFirstRun = context.IsFirstRun; Owner = context.Owner; }
        internal static void Reset() { RunCount = 0; WasFirstRun = false; Owner = null; }
    }
}
