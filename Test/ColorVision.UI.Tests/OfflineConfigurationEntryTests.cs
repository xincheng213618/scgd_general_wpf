using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using FlowEngineLib.End;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class OfflineConfigurationEntryTests
{
    [Fact]
    public void LocalSpectrumDeviceAndWindowOpenWithoutLoadingMysqlTemplates()
    {
        WithWorkspace((_, _) =>
        {
            using var device = new ColorVision.Engine.Services.Devices.Spectrum.DeviceSpectrum(new ColorVision.Engine.SysResourceModel
            {
                Id = -77, Code = "local-spectrum-ui", Name = "本地光谱仪", Value = "{}"
            });
            Assert.True(device.DisplayConfig.UseLocalSpectrum);
            Assert.Empty(device.SpectrumResourceParams);
            device.Config.NDConfig.IsBingNDDevice = true;
            Assert.False(device.TrySetNDPortForCalibrationGroup(1, out var ndCommand, out var ndError));
            Assert.Null(ndCommand);
            Assert.Contains("本地", ndError);
            Assert.Equal(ColorVision.Engine.Services.DeviceStatusType.Closed, device.DService.DeviceStatus);
            var window = new ColorVision.Engine.Services.Devices.Spectrum.Local.LocalSpectrumWindow(device);
            try
            {
                var node = new ColorVision.Engine.FlowProcessing.Nodes.LocalSpectrumNode();
                window.AttachNode(node);
                window.Measure(new Size(900, 650));
                window.Arrange(new Rect(0, 0, 900, 650));
                var button = Assert.IsType<Button>(window.FindName("ApplyNodeButton"));
                Assert.Equal(Visibility.Visible, button.Visibility);
                device.DisplayConfig.IntTime = 321;
                device.DisplayConfig.AveNum = 4;
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(321, node.IntegralTime);
                Assert.Equal(4, node.NumberOfAverage);
                Assert.False(device.LocalSession.IsOpen);
            }
            finally { window.Close(); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyOfflineWorkspaceBindsToolbarCommandsAndOpensTemplateManager(bool useCanvasMenu)
    {
        WithWorkspace((manager, storage) =>
        {
            Assert.Empty(manager.FlowParams);
            var toolbar = Assert.IsType<StackPanel>(manager.View.EditorCanvas.ToolbarContent);
            Assert.Same(manager.View.NewFlowCommand, Assert.IsType<Button>(toolbar.Children[0]).Command);
            Assert.Same(manager.View.SaveCommand, Assert.IsType<Button>(toolbar.Children[1]).Command);
            Assert.All(toolbar.Children.OfType<Button>(), button => Assert.NotNull(button.Command));
            Button edit = Descendants(manager.DisplayFlow).OfType<Button>()
                .Single(button => ReferenceEquals(button.Command, manager.EditTemplateFlowCommand));
            Assert.True(edit.IsEnabled);
            WithDialog<TemplateEditorWindow>(dialog =>
            {
                Assert.Equal(0, dialog.ITemplate.Count);
                Assert.Contains("本地", dialog.Title);
                var createTarget = Assert.IsType<Grid>(dialog.FindName("MainGrid"));
                WithDialog<TemplateCreate>(create => CreateTemplate(create, "管理器首个流程"),
                    () => System.Windows.Input.ApplicationCommands.New.Execute(null, createTarget));
            }, () => (useCanvasMenu ? manager.View.OpenFlowTemplateCommand : edit.Command).Execute(null));
            Assert.Equal(Assert.Single(storage.Load()).Id, manager.SelectedFlowParam?.Id);
        });
    }

    [Fact]
    public void FirstSaveCreatesNamedLocalFlowWithoutReplacingCanvasAndNextSaveUpdatesIt()
    {
        WithWorkspace((manager, storage) =>
        {
            AddCanvas(manager.View);
            var node = manager.View.STNodeEditorMain.Nodes[0];
            WithDialog<TemplateCreate>(dialog => CreateTemplate(dialog, "首次保存"), () => Assert.True(manager.View.TrySave()));
            FlowParam created = Assert.Single(storage.Load());
            Assert.True(LocalFlowTemplateStorage.IsLocalId(created.Id));
            Assert.Same(node, manager.View.STNodeEditorMain.Nodes[0]);
            Assert.Equal(created.Id, manager.SelectedFlowParam?.Id);
            Assert.Equal(0, manager.TemplateFlowParamsIndex);
            node.Left = 170;
            Assert.True(manager.View.TrySave());
            using var reopened = new STNodeEditor();
            reopened.LoadCanvas(Convert.FromBase64String(Assert.Single(storage.Load()).DataBase64));
            Assert.Equal(170, reopened.Nodes[0].Left);
            Assert.Single(reopened.GetConnectionInfo());
        });
    }

    [Fact]
    public void FirstSaveStaysLocalWhenConnectionChangesInsideNamingDialog()
    {
        bool connected = false;
        WithWorkspace((manager, storage) =>
        {
            AddCanvas(manager.View);
            WithDialog<TemplateCreate>(dialog =>
            {
                Assert.Contains("本地", dialog.Title);
                connected = true;
                CreateTemplate(dialog, "本地保存期间重连");
            }, () => Assert.True(manager.View.TrySave()));
            Assert.Equal(Assert.Single(storage.Load()).Id, manager.SelectedFlowParam?.Id);
        }, () => connected);
    }

    [Fact]
    public void CancelFirstSavePreservesUnsavedCanvasAndCreatesNoRecord()
    {
        WithWorkspace((manager, storage) =>
        {
            AddCanvas(manager.View);
            byte[] before = manager.View.STNodeEditorMain.GetCanvasData();
            WithDialog<TemplateCreate>(_ => { }, () => Assert.False(manager.View.TrySave()));
            Assert.Empty(storage.Load());
            Assert.Null(manager.SelectedFlowParam);
            Assert.Equal(before, manager.View.STNodeEditorMain.GetCanvasData());
        });
    }

    [Fact]
    public void NewToolbarButtonCreatesAndSelectsFirstLocalTemplate()
    {
        WithWorkspace((manager, storage) =>
        {
            var toolbar = Assert.IsType<StackPanel>(manager.View.EditorCanvas.ToolbarContent);
            Button create = Assert.IsType<Button>(toolbar.Children[0]);
            WithDialog<TemplateCreate>(dialog => CreateTemplate(dialog, "新建流程"), () => create.Command.Execute(null));
            Assert.Equal(Assert.Single(storage.Load()).Id, manager.SelectedFlowParam?.Id);
            AddCanvas(manager.View);
            Assert.True(manager.View.TrySave());
            Assert.NotEmpty(Assert.Single(storage.Load()).DataBase64);
        });
    }

    [Fact]
    public void DevicePanelCreateActionDispatchesToConfiguredDeviceUi()
    {
        WpfTestHost.Invoke(() =>
        {
            var manager = DisPlayManager.GetInstance();
            var previous = manager.DeviceConfigurationCommand;
            try
            {
                int opened = 0;
                manager.DeviceConfigurationCommand = new RelayCommand(_ => opened++);
                var panel = new DisPlayControlPanel();
                var action = panel.TitleActions[0];
                Assert.True(action.Command.CanExecute(null));
                action.Command.Execute(null);
                Assert.Equal(1, opened);
            }
            finally { manager.DeviceConfigurationCommand = previous; }
        });
    }

    private static void AddCanvas(ViewFlow view)
    {
        var start = new HeadlessTestStartNode();
        var end = new CVEndNode();
        start.Create();
        end.Create();
        start.Left = 20;
        end.Left = 300;
        view.STNodeEditorMain.Nodes.AddRange([start, end]);
        Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(end.m_in_start));
    }

    private static void CreateTemplate(TemplateCreate dialog, string name)
    {
        var view = Assert.IsType<TemplateCreateView>(dialog.Content);
        Assert.IsAssignableFrom<TextBox>(view.FindName("CreateNameTextBox")).Text = name;
        Descendants(view).OfType<Button>().Single(button => Equals(button.Content, ColorVision.Engine.Properties.Resources.Create))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(name, dialog.CreateName);
    }

    private static void WithDialog<T>(Action<T> interact, Action open) where T : Window
    {
        bool opened = false;
        Exception? error = null;
        DispatcherOperation operation = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            T? dialog = Application.Current.Windows.OfType<T>().FirstOrDefault();
            try
            {
                Assert.NotNull(dialog);
                opened = true;
                interact(dialog);
            }
            catch (Exception ex) { error = ex; }
            finally { dialog?.Close(); }
        });
        try { open(); }
        finally { operation.Abort(); }
        Assert.True(opened, $"The command did not open {typeof(T).Name}.");
        Assert.Null(error);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject element)
            {
                yield return element;
                foreach (var descendant in Descendants(element)) yield return descendant;
            }
    }

    private static void WithWorkspace(Action<FlowEngineManager, LocalFlowTemplateStorage> action, Func<bool>? isConnected = null)
    {
        WpfTestHost.Invoke(() =>
        {
            var previousTemplates = TemplateFlow.Params;
            var previousRegistrations = TemplateControl.ITemplateNames.ToArray();
            var previousConfig = ConfigService.Instance;
            ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
            string previousAppData = Environments.DirAppData;
            int previousIndex = FlowEngineConfig.Instance.TemplateFlowParamsIndex;
            int previousFlow = FlowEngineConfig.Instance.LastSelectFlow;
            string directory = Path.Combine(Path.GetTempPath(), "ColorVision-offline-entry-tests", Guid.NewGuid().ToString("N"));
            ResourceDictionary resources = Application.Current.Resources;
            var previousResources = resources.Keys.Cast<object>().ToDictionary(key => key, key => resources[key]);
            var previousDictionaries = resources.MergedDictionaries.ToArray();
            var dictionaries = new List<ResourceDictionary>();
            FlowEngineManager? manager = null;
            Window? previousWindow = Application.Current.MainWindow;
            var owner = new Window { ShowActivated = false, ShowInTaskbar = false, Width = 1, Height = 1, Left = -10000, Top = -10000 };
            try
            {
                resources.Clear();
                resources.MergedDictionaries.Clear();
                Application.Current.MainWindow = owner;
                owner.Show();
                TemplateFlow.Params = [];
                Environments.DirAppData = directory;
                FlowEngineConfig.Instance.TemplateFlowParamsIndex = -1;
                FlowEngineConfig.Instance.LastSelectFlow = -1;
                foreach (string source in new[]
                {
                    "/HandyControl;component/Themes/basic/colors/colorsdark.xaml",
                    "/HandyControl;component/Themes/Theme.xaml",
                    "/ColorVision.Themes;component/Themes/Dark.xaml",
                    "/ColorVision.Themes;component/Themes/Base.xaml"
                })
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                    dictionaries.Add(dictionary);
                }
                var storage = new LocalFlowTemplateStorage(new LocalTemplateStore(Path.Combine(directory, "ColorVision.Local.db")));
                manager = new FlowEngineManager(() => new TemplateFlow(storage, isConnected ?? (() => false),
                    () => throw new InvalidOperationException("Offline UI must not open MySQL.")));
                manager.View.Measure(new Size(1000, 700));
                manager.View.Arrange(new Rect(0, 0, 1000, 700));
                manager.View.UpdateLayout();
                manager.DisplayFlow.Measure(new Size(300, 200));
                manager.DisplayFlow.Arrange(new Rect(0, 0, 300, 200));
                manager.DisplayFlow.UpdateLayout();
                var frame = new DispatcherFrame();
                // Drain binding/layout work, without waiting for unrelated image-view timers to become idle.
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Loaded, () => frame.Continue = false);
                Dispatcher.PushFrame(frame);
                action(manager, storage);
            }
            finally
            {
                manager?.DisplayFlow.Dispose();
                if (manager != null) ColorVision.UI.Views.DockViewManager.GetInstance().RemoveView(manager.View);
                manager?.View.Dispose();
                manager?.FlowEngineControl.Dispose();
                owner.Close();
                Application.Current.MainWindow = previousWindow;
                TemplateFlow.Params = previousTemplates;
                TemplateControl.ITemplateNames.Clear();
                foreach (var registration in previousRegistrations) TemplateControl.ITemplateNames.Add(registration.Key, registration.Value);
                Environments.DirAppData = previousAppData;
                FlowEngineConfig.Instance.TemplateFlowParamsIndex = previousIndex;
                FlowEngineConfig.Instance.LastSelectFlow = previousFlow;
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                resources.Clear();
                foreach (var resource in previousResources) resources[resource.Key] = resource.Value;
                foreach (var dictionary in previousDictionaries) resources.MergedDictionaries.Add(dictionary);
                ConfigService.SetInstance(previousConfig!);
            }
        });
    }
}
