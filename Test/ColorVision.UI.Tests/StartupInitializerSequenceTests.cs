using ColorVision.Themes;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class StartupInitializerSequenceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly FieldInfo ConfigInstanceField = RequiredField(typeof(ConfigHandler), "_instance", isStatic: true);
    private static readonly FieldInfo AttemptCompletedField = RequiredField(typeof(StartupRegistryChecker), "_attemptCompleted", isStatic: true);

    [Fact]
    public async Task PendingInitializerAllowsStatusUpdatesAndLaterInitializersWaitAndSurviveFailure()
    {
        var calls = new ConcurrentQueue<string>();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        List<IInitializer> initializers =
        [
            new CallbackInitializer("first", async () =>
            {
                calls.Enqueue("first-start");
                firstStarted.SetResult();
                await finishFirst.Task.ConfigureAwait(false);
                calls.Enqueue("first-end");
            }),
            new CallbackInitializer("failing", async () =>
            {
                calls.Enqueue("failing");
                await Task.Yield();
                throw new InvalidOperationException("Synthetic initializer failure.");
            }),
            new CallbackInitializer("last", () =>
            {
                calls.Enqueue("last");
                return Task.CompletedTask;
            }),
        ];

        await WithIsolatedWindowAsync(initializers, async window =>
        {
            Task sequence = Task.Run(() => InvokeInitializerSequence(window));
            try
            {
                await firstStarted.Task.WaitAsync(Timeout);
                WpfTestHost.Invoke(() =>
                {
                    PumpDispatcher();
                    Assert.Equal("正在加载扩展组件", Status(window).Text);
                    Assert.True(ProgressTimer(window).IsEnabled);
                    Assert.False(sequence.IsCompleted);
                    Assert.Equal(["first-start"], calls.ToArray());
                });

                finishFirst.SetResult();
                await sequence.WaitAsync(Timeout);

                Assert.Equal(["first-start", "first-end", "failing", "last"], calls.ToArray());
                WpfTestHost.Invoke(() => AssertCompletedAfterPendingMessages(window, initializers.Count));
            }
            finally
            {
                finishFirst.TrySetResult();
                // Finish worker callbacks before restoring the process-wide test fixtures.
                await sequence.WaitAsync(Timeout);
            }
        });
    }

    [Fact]
    public async Task PrerequisiteLanesOverlapAndJoinBeforeTemplate()
    {
        var calls = new ConcurrentQueue<string>();
        var mysqlStarted = NewSignal();
        var finishMysql = NewSignal();
        var workspaceStarted = NewSignal();
        var finishWorkspace = NewSignal();
        var workspaceUiApplied = NewSignal();
        var mqttStarted = NewSignal();
        var finishMqtt = NewSignal();
        var templateStarted = NewSignal();
        var finishTemplate = NewSignal();
        var rcStarted = NewSignal();
        var finishRc = NewSignal();
        var serviceStarted = NewSignal();

        List<IInitializer> initializers =
        [
            new CallbackMySqlInitializer(async () =>
            {
                calls.Enqueue("mysql-start");
                mysqlStarted.SetResult();
                await finishMysql.Task.ConfigureAwait(false);
                calls.Enqueue("mysql-end");
            }),
            new CallbackSolutionManagerInitializer(async () =>
            {
                calls.Enqueue("workspace-start");
                workspaceStarted.SetResult();
                await finishWorkspace.Task.ConfigureAwait(false);
                calls.Enqueue("workspace-end");
                _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    calls.Enqueue("workspace-ui");
                    workspaceUiApplied.SetResult();
                }));
            }),
            new CallbackMqttInitializer(async () =>
            {
                calls.Enqueue("mqtt-start");
                mqttStarted.SetResult();
                await finishMqtt.Task.ConfigureAwait(false);
                calls.Enqueue("mqtt-failed");
                throw new InvalidOperationException("Synthetic MQTT failure.");
            }),
            new CallbackRcInitializer(async () =>
            {
                calls.Enqueue("rc-start");
                rcStarted.SetResult();
                await finishRc.Task.ConfigureAwait(false);
                calls.Enqueue("rc-end");
            }),
            new CallbackTemplateInitializer(async () =>
            {
                calls.Enqueue("template-start");
                templateStarted.SetResult();
                await finishTemplate.Task.ConfigureAwait(false);
                calls.Enqueue("template-end");
            }),
            new CallbackServiceInitializer(() =>
            {
                calls.Enqueue("service-start");
                serviceStarted.SetResult();
                return Task.CompletedTask;
            }),
        ];

        await WithIsolatedWindowAsync(initializers, async window =>
        {
            Task sequence = Task.Run(() => InvokeInitializerSequence(window));
            try
            {
                await Task.WhenAll(mysqlStarted.Task, workspaceStarted.Task, mqttStarted.Task).WaitAsync(Timeout);
                Assert.False(rcStarted.Task.IsCompleted);
                Assert.False(templateStarted.Task.IsCompleted);

                finishMqtt.SetResult();
                await rcStarted.Task.WaitAsync(Timeout);
                Assert.False(templateStarted.Task.IsCompleted);

                finishMysql.SetResult();
                finishWorkspace.SetResult();
                Task prematureTemplate = await Task.WhenAny(templateStarted.Task, Task.Delay(100));
                Assert.NotSame(templateStarted.Task, prematureTemplate);
                Assert.False(serviceStarted.Task.IsCompleted);

                finishRc.SetResult();
                await workspaceUiApplied.Task.WaitAsync(Timeout);
                await templateStarted.Task.WaitAsync(Timeout);
                Assert.False(serviceStarted.Task.IsCompleted);

                finishTemplate.SetResult();
                await serviceStarted.Task.WaitAsync(Timeout);
                await sequence.WaitAsync(Timeout);

                string[] orderedCalls = calls.ToArray();
                Assert.True(Array.IndexOf(orderedCalls, "mqtt-start") < Array.IndexOf(orderedCalls, "mysql-end"));
                Assert.True(Array.IndexOf(orderedCalls, "workspace-start") < Array.IndexOf(orderedCalls, "mysql-end"));
                Assert.True(Array.IndexOf(orderedCalls, "rc-start") > Array.IndexOf(orderedCalls, "mqtt-failed"));
                Assert.True(Array.IndexOf(orderedCalls, "template-start") > Array.IndexOf(orderedCalls, "mysql-end"));
                Assert.True(Array.IndexOf(orderedCalls, "template-start") > Array.IndexOf(orderedCalls, "workspace-end"));
                Assert.True(Array.IndexOf(orderedCalls, "template-start") > Array.IndexOf(orderedCalls, "workspace-ui"));
                Assert.True(Array.IndexOf(orderedCalls, "template-start") > Array.IndexOf(orderedCalls, "rc-end"));
                Assert.True(Array.IndexOf(orderedCalls, "service-start") > Array.IndexOf(orderedCalls, "template-end"));
                WpfTestHost.Invoke(() => AssertCompletedAfterPendingMessages(window, initializers.Count));
            }
            finally
            {
                finishMysql.TrySetResult();
                finishWorkspace.TrySetResult();
                finishMqtt.TrySetResult();
                finishTemplate.TrySetResult();
                finishRc.TrySetResult();
                await sequence.WaitAsync(Timeout);
            }
        });
    }

    [Fact]
    public async Task UnknownInitializersKeepTheKnownLanesSerial()
    {
        var calls = new ConcurrentQueue<string>();
        var mysqlStarted = NewSignal();
        var finishMysql = NewSignal();
        var workspaceStarted = NewSignal();
        var mqttStarted = NewSignal();
        var finishMqtt = NewSignal();
        var rcStarted = NewSignal();

        List<IInitializer> initializers =
        [
            new CallbackMySqlInitializer(async () =>
            {
                calls.Enqueue("mysql-start");
                mysqlStarted.SetResult();
                await finishMysql.Task.ConfigureAwait(false);
                calls.Enqueue("mysql-end");
            }),
            new CallbackInitializer("database-extension", () =>
            {
                calls.Enqueue("database-extension");
                return Task.CompletedTask;
            }),
            new CallbackSolutionManagerInitializer(() =>
            {
                calls.Enqueue("workspace");
                workspaceStarted.SetResult();
                return Task.CompletedTask;
            }),
            new CallbackMqttInitializer(async () =>
            {
                calls.Enqueue("mqtt-start");
                mqttStarted.SetResult();
                await finishMqtt.Task.ConfigureAwait(false);
                calls.Enqueue("mqtt-end");
            }),
            new CallbackInitializer("connectivity-extension", () =>
            {
                calls.Enqueue("connectivity-extension");
                return Task.CompletedTask;
            }),
            new CallbackRcInitializer(() =>
            {
                calls.Enqueue("rc");
                rcStarted.SetResult();
                return Task.CompletedTask;
            }),
            new CallbackTemplateInitializer(() =>
            {
                calls.Enqueue("template");
                return Task.CompletedTask;
            }),
        ];

        await WithIsolatedWindowAsync(initializers, async window =>
        {
            Task sequence = Task.Run(() => InvokeInitializerSequence(window));
            try
            {
                await mysqlStarted.Task.WaitAsync(Timeout);
                await Task.Delay(100);
                Assert.False(workspaceStarted.Task.IsCompleted);

                finishMysql.SetResult();
                await mqttStarted.Task.WaitAsync(Timeout);
                Assert.False(rcStarted.Task.IsCompleted);

                finishMqtt.SetResult();
                await sequence.WaitAsync(Timeout);

                Assert.Equal(
                [
                    "mysql-start",
                    "mysql-end",
                    "database-extension",
                    "workspace",
                    "mqtt-start",
                    "mqtt-end",
                    "connectivity-extension",
                    "rc",
                    "template",
                ], calls.ToArray());
                WpfTestHost.Invoke(() => AssertCompletedAfterPendingMessages(window, initializers.Count));
            }
            finally
            {
                finishMysql.TrySetResult();
                finishMqtt.TrySetResult();
                await sequence.WaitAsync(Timeout);
            }
        });
    }

    [Fact]
    public async Task PendingStageUpdatesCannotOverwriteTheFinalState()
    {
        List<IInitializer> initializers =
        [
            new CallbackInitializer("first", () => Task.CompletedTask),
            new CallbackInitializer("second", () => Task.CompletedTask),
        ];

        await WithIsolatedWindowAsync(initializers, async window =>
        {
            Task? completion = null;
            WpfTestHost.Invoke(() =>
            {
                // Queue real progress callbacks and completion in one dispatcher turn, as
                // short final initializers can do. This avoids depending on the loop's
                // elapsed-time threshold for its cooperative Background checkpoints.
                MethodInfo update = RequiredMethod("UpdateStartupProgress");
                update.Invoke(window, [0d, true, 1d, "正在连接基础服务"]);
                update.Invoke(window, [1d, true, 1d, "正在加载扩展组件"]);
                completion = Assert.IsAssignableFrom<Task>(RequiredMethod("CompleteStartupProgressAsync").Invoke(window, null));
                Assert.False(completion.IsCompleted, "Completion must await its queued UI transition.");

                PumpDispatcher();
                Assert.True(completion.IsCompletedSuccessfully);
                AssertCompletedAfterPendingMessages(window, initializers.Count);
            });
            await completion!.WaitAsync(Timeout);
        });
    }

    private static Task InvokeInitializerSequence(StartWindow window) =>
        Assert.IsAssignableFrom<Task>(RequiredMethod("InitializedOver").Invoke(window, null));

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void AssertCompletedAfterPendingMessages(StartWindow window, int stepCount)
    {
        PumpDispatcher();
        Assert.Equal("正在打开工作空间", Status(window).Text);
        ProgressBar progress = Assert.IsType<ProgressBar>(window.FindName("startupProgressBar"));
        Assert.Equal((double)stepCount, progress.Maximum);
        Assert.Equal(progress.Maximum, progress.Value);
        Assert.False(ProgressTimer(window).IsEnabled);
    }

    private static TextBlock Status(StartWindow window) => Assert.IsType<TextBlock>(window.FindName("startupStatusText"));

    private static DispatcherTimer ProgressTimer(StartWindow window) =>
        Assert.IsType<DispatcherTimer>(RequiredField(typeof(StartWindow), "_startupProgressTimer").GetValue(window));

    private static async Task WithIsolatedWindowAsync(List<IInitializer> initializers, Func<StartWindow, Task> action)
    {
        string root = Directory.CreateTempSubdirectory("ColorVisionStartupSequence-").FullName;
        CultureInfo previousCallerCulture = CultureInfo.CurrentUICulture;
        CultureInfo? previousDispatcherCulture = null;
        StartWindow? window = null;
        ResourceDictionary? previousResources = null;
        Window? previousMainWindow = null;
        IConfigService? previousConfigService = null;
        object? previousConfigInstance = null;
        object? previousAttemptCompleted = null;
        InitAppender? previousAppender = null;
        ThemeManager? previousManager = null;
        ThemeManager? manager = null;
        bool capturedState = false;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
            // Do not flow the caller's culture while capturing the WPF thread's own
            // original culture. Worker tasks below inherit the explicit test culture.
            InvokeWithoutCallerContext(() =>
            {
                previousDispatcherCulture = CultureInfo.CurrentUICulture;
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                previousResources = Application.Current.Resources;
                previousMainWindow = Application.Current.MainWindow;
                previousConfigService = ConfigService.Instance;
                previousConfigInstance = ConfigInstanceField.GetValue(null);
                previousAttemptCompleted = AttemptCompletedField.GetValue(null);
                previousAppender = ProgramTimer.InitAppender;
                previousManager = ThemeManager.Current;
                capturedState = true;

                // MarkStage returns before accessing the registry for a completed attempt.
                // The only explicit config save writes the startup profile into this temp file.
                AttemptCompletedField.SetValue(null, true);
                var config = new ConfigHandler
                {
                    ConfigFilePath = Path.Combine(root, "ColorVisionConfig.json"),
                    IsAutoSave = false,
                };
                ConfigInstanceField.SetValue(null, config);
                ConfigService.SetInstance(config);
                ProgramTimer.InitAppender = null!;
                Application.Current.Resources = new ResourceDictionary();
                manager = new ThemeManager();
                ThemeManager.Current = manager;
                manager.ApplyTheme(Application.Current, Theme.Dark);

                // Do not Show: ContentRendered launches real discovery and the main window.
                // Exercise the real worker sequence and its dispatcher updates directly.
                window = new StartWindow();
                RequiredField(typeof(StartWindow), "_IComponentInitializers").SetValue(window, initializers);
                RequiredField(typeof(StartWindow), "_startupTotalSteps").SetValue(window, initializers.Count);
                RequiredMethod("LoadStartupProgressProfile").Invoke(window, null);
            });
            await action(window!);
        }
        finally
        {
            try
            {
                InvokeWithoutCallerContext(() =>
                {
                    try
                    {
                        if (!capturedState)
                            return;
                        try
                        {
                            try
                            {
                                PumpDispatcher();
                            }
                            finally
                            {
                                // Drain queued progress before Close stops its timer, including
                                // when an assertion failed before the completion callback ran.
                                window?.Close();
                            }
                        }
                        finally
                        {
                            manager?.Dispose();
                            Application.Current.MainWindow = previousMainWindow;
                            Application.Current.Resources = previousResources!;
                            ThemeManager.Current = previousManager!;
                            ProgramTimer.InitAppender = previousAppender!;
                            ConfigInstanceField.SetValue(null, previousConfigInstance);
                            ConfigService.SetInstance(previousConfigService!);
                            AttemptCompletedField.SetValue(null, previousAttemptCompleted);
                        }
                    }
                    finally
                    {
                        if (previousDispatcherCulture != null)
                            CultureInfo.CurrentUICulture = previousDispatcherCulture;
                    }
                });
            }
            finally
            {
                CultureInfo.CurrentUICulture = previousCallerCulture;
                string cleanupRoot = Path.GetFullPath(root);
                string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!cleanupRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Startup sequence test cleanup must stay inside the temporary directory.");
                Directory.Delete(cleanupRoot, recursive: true);
            }
        }
    }

    private static void InvokeWithoutCallerContext(Action action)
    {
        using (ExecutionContext.SuppressFlow())
            WpfTestHost.Invoke(action);
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static MethodInfo RequiredMethod(string name) =>
        typeof(StartWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Missing StartWindow method {name}.");

    private static FieldInfo RequiredField(Type type, string name, bool isStatic = false) =>
        type.GetField(name, BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance))
        ?? throw new InvalidOperationException($"Missing {type.Name} field {name}.");

    private sealed class CallbackInitializer(string name, Func<Task> initialize) : InitializerBase
    {
        public override string Name => name;
        public override Task InitializeAsync() => initialize();
    }

    private sealed class CallbackSolutionManagerInitializer(Func<Task> initialize)
        : global::ColorVision.Solution.SolutionManagerInitializer
    {
        public override Task InitializeAsync() => initialize();
    }

    private sealed class CallbackMySqlInitializer(Func<Task> initialize)
        : global::ColorVision.Engine.MySqlInitializer
    {
        public override Task InitializeAsync() => initialize();
    }

    private sealed class CallbackMqttInitializer(Func<Task> initialize)
        : global::ColorVision.Engine.MQTT.MqttInitializer
    {
        public override Task InitializeAsync() => initialize();
    }

    private sealed class CallbackRcInitializer(Func<Task> initialize)
        : global::ColorVision.Engine.Services.RC.RCInitializer
    {
        public override Task InitializeAsync() => initialize();
    }

    private sealed class CallbackTemplateInitializer(Func<Task> initialize)
        : global::ColorVision.Engine.Templates.TemplateInitializer
    {
        public override Task InitializeAsync() => initialize();
    }

    private sealed class CallbackServiceInitializer(Func<Task> initialize)
        : global::ColorVision.Engine.Services.ServiceInitializer
    {
        public override Task InitializeAsync() => initialize();
    }
}
