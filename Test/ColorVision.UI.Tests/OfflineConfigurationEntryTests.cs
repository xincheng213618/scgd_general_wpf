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
            WithDialog<FlowTemplateManagerWindow>(dialog =>
            {
                Assert.Empty(Assert.IsType<ListView>(dialog.FindName("TemplateList")).Items);
                Assert.Contains("本地", dialog.Title);
                WithDialog<TemplateCreate>(create => CreateTemplate(create, "管理器首个流程"),
                    () => System.Windows.Input.ApplicationCommands.New.Execute(null, dialog));
            }, () => (useCanvasMenu ? manager.View.OpenFlowTemplateCommand : edit.Command).Execute(null));
            Assert.Equal(Assert.Single(storage.Load()).Id, manager.SelectedFlowParam?.Id);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void OpeningAndClosingTemplateManagerPreservesWorkflowComboSelection(int selectedIndex)
    {
        WithWorkspace((manager, storage) =>
        {
            foreach (string name in new[] { "White51_Test", "Chessboard_ANSI_Test", "OpticCenter_Test" })
                storage.Save(new FlowParam { Name = name });
            manager.CreateFlowTemplate().Load();
            var combo = Assert.IsType<ComboBox>(manager.DisplayFlow.FindName("ComboBoxFlow"));
            combo.SelectedIndex = selectedIndex;
            object selectedItem = combo.SelectedItem;
            string selectedName = manager.FlowParams[selectedIndex].Key;

            void AssertSelection()
            {
                Assert.Equal(3, combo.Items.Count);
                Assert.Equal(selectedIndex, combo.SelectedIndex);
                Assert.Same(selectedItem, combo.SelectedItem);
                Assert.Equal(selectedName, combo.Text);
                Assert.Equal(selectedIndex, manager.TemplateFlowParamsIndex);
            }

            AssertSelection();
            WithDialog<FlowTemplateManagerWindow>(_ => AssertSelection(), () => manager.EditTemplateFlowCommand.Execute(null));
            AssertSelection();
        });
    }

    [Fact]
    public void FlowBrowserSharesSelectionOrderAndFilterAcrossModesWithoutReloadingRuntimeSelection()
    {
        WithWorkspace((manager, storage) =>
        {
            AddCanvas(manager.View);
            string canvas = Convert.ToBase64String(manager.View.STNodeEditorMain.GetCanvasData());
            foreach (string name in new[] { "White51_Test", "White255_Test", "Chessboard_ANSI_Test", "OpticCenter_Test", "Distortion_Test", "MTF_HV_1_Test" })
                storage.Save(new FlowParam { Name = name, DataBase64 = canvas });
            manager.CreateFlowTemplate().Load();
            var combo = Assert.IsType<ComboBox>(manager.DisplayFlow.FindName("ComboBoxFlow"));
            combo.SelectedIndex = 2;
            object runtimeSelection = combo.SelectedItem;
            var template = new BrowserTestFlow(storage);
            var cache = new FlowTemplateCoverService(Path.Combine(Environments.DirAppData, "cover-test.db"));
            var dialog = new FlowTemplateManagerWindow(template, 2, cache) { Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false };
            try
            {
                dialog.Show();
                dialog.UpdateLayout();
                Assert.True(dialog.IsTileMode);
                double width = dialog.Width;
                var list = Assert.IsType<ListView>(dialog.FindName("TemplateList"));
                var search = Assert.IsType<TextBox>(dialog.FindName("SearchBox"));
                object[] order = list.Items.Cast<object>().ToArray();
                object selection = list.SelectedItem;
                var marked = Assert.IsType<TemplateModel<FlowParam>>(order[1]);
                marked.IsSelected = true;
                dialog.SetViewMode(true);
                DrainBrowser();
                Assert.True(dialog.IsTileMode);
                Assert.Equal(order, list.Items.Cast<object>());
                Assert.Same(selection, list.SelectedItem);
                Assert.Same(runtimeSelection, combo.SelectedItem);
                Assert.Equal(1, template.LoadCount);
                Assert.True(marked.IsSelected);
                Assert.Equal(order.Length, VisualChildren(list).OfType<FlowTemplateCover>().Count());
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (DateTime.UtcNow < deadline && VisualChildren(list).OfType<Image>().Count(image => image.Source != null) < order.Length)
                {
                    DrainBrowser();
                    Thread.Sleep(10);
                }
                Assert.Equal(order.Length, VisualChildren(list).OfType<Image>().Count(image => image.Source != null));
                CaptureBrowser(dialog, "flow-tiles.png");
                var targetCard = Assert.IsType<ListViewItem>(list.ItemContainerGenerator.ContainerFromIndex(3));
                targetCard.Tag = "SwapTarget";
                CaptureBrowser(dialog, "flow-drag-target.png");
                targetCard.Tag = null;
                dialog.Width = 640;
                DrainBrowser();
                CaptureBrowser(dialog, "flow-compact.png");
                dialog.Width = width;
                DrainBrowser();
                search.Text = "OpticCenter";
                dialog.UpdateLayout();
                var container = Assert.IsType<ListViewItem>(list.ItemContainerGenerator.ContainerFromIndex(0));
                list.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                {
                    RoutedEvent = Control.PreviewMouseDoubleClickEvent, Source = container
                });
                Assert.Equal(3, template.OpenedIndex);
                dialog.SetViewMode(false);
                DrainBrowser();
                Assert.Equal(width, dialog.Width);
                Assert.Single(list.Items);
                Assert.Equal("OpticCenter", search.Text);
                Assert.Equal(1, template.LoadCount);
                Assert.Same(runtimeSelection, combo.SelectedItem);
                search.Clear();
                Assert.Equal(order, list.Items.Cast<object>());
                CaptureBrowser(dialog, "flow-list.png");
                var more = Assert.IsType<ContextMenu>(dialog.Resources["MoreMenu"]);
                WithDialog<TemplateEditorWindow>(_ => Assert.Same(runtimeSelection, combo.SelectedItem),
                    () => Assert.IsType<MenuItem>(more.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)));
                Assert.Same(runtimeSelection, combo.SelectedItem);
            }
            finally { dialog.Close(); }
        });
    }

    private sealed class BrowserTestFlow(LocalFlowTemplateStorage storage) : TemplateFlow(storage, () => false)
    {
        public int LoadCount { get; private set; }
        public int OpenedIndex { get; private set; } = -1;
        public override void Load() { LoadCount++; base.Load(); }
        public override void PreviewMouseDoubleClick(int index) => OpenedIndex = index;
        public override bool SwapTemplateOrder(int index1, int index2) => throw new InvalidOperationException("Browser dragging must not reorder server templates.");
    }

    [Fact]
    public void FlowBrowserRestoresEachModesScrollPositionAndKeepsSourceOrder()
    {
        WithWorkspace((_, storage) =>
        {
            for (int i = 0; i < 60; i++) storage.Save(new FlowParam { Name = $"Flow_{i:00}" });
            var template = new BrowserTestFlow(storage);
            var dialog = new FlowTemplateManagerWindow(template, 0) { Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false };
            try
            {
                dialog.Show();
                dialog.SetViewMode(false);
                DrainBrowser();
                var list = Assert.IsType<ListView>(dialog.FindName("TemplateList"));
                var scroll = Assert.IsType<ScrollViewer>(FlowTemplateManagerWindow.FindChild<ScrollViewer>(list));
                scroll.ScrollToVerticalOffset(15);
                DrainBrowser();
                double listOffset = scroll.VerticalOffset;
                Assert.True(listOffset > 0);
                dialog.SetViewMode(true);
                DrainBrowser();
                scroll = Assert.IsType<ScrollViewer>(FlowTemplateManagerWindow.FindChild<ScrollViewer>(list));
                scroll.ScrollToVerticalOffset(280);
                DrainBrowser();
                double tileOffset = scroll.VerticalOffset;
                Assert.True(tileOffset > 0);
                dialog.SetViewMode(false);
                DrainBrowser();
                Assert.Equal(listOffset, FlowTemplateManagerWindow.FindChild<ScrollViewer>(list)!.VerticalOffset);
                dialog.SetViewMode(true);
                DrainBrowser();
                Assert.Equal(tileOffset, FlowTemplateManagerWindow.FindChild<ScrollViewer>(list)!.VerticalOffset);
                Assert.Equal(template.TemplateParams, list.Items.Cast<TemplateModel<FlowParam>>());
                Assert.Equal(1, template.LoadCount);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void BrowserDragSwapsOnlyTwoSlotsPreservesRuntimeAndPersistsAcrossWindows()
    {
        WithWorkspace((manager, storage) =>
        {
            foreach (string name in new[] { "Flow_A", "Flow_B", "Flow_C", "Flow_D" }) storage.Save(new FlowParam { Name = name });
            manager.CreateFlowTemplate().Load();
            var combo = Assert.IsType<ComboBox>(manager.DisplayFlow.FindName("ComboBoxFlow"));
            combo.SelectedIndex = 1;
            object selected = combo.SelectedItem;
            var template = new BrowserTestFlow(storage);
            var cache = new FlowTemplateCoverService(Path.Combine(Environments.DirAppData, "browser.db"));
            var dialog = new FlowTemplateManagerWindow(template, 1, cache) { Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false };
            var original = template.TemplateParams.ToArray();
            int[] ids = original.Select(item => item.Id).ToArray();
            try
            {
                dialog.Show();
                WaitForBrowser(dialog.OrderLoadTask);
                var list = Assert.IsType<ListView>(dialog.FindName("TemplateList"));
                Task first = dialog.SwapItemsAsync(original[0], original[3]);
                Assert.Equal(new[] { original[3], original[1], original[2], original[0] }, list.Items.Cast<TemplateModel<FlowParam>>());
                Assert.Equal(original, template.TemplateParams);
                Assert.Equal(ids, template.TemplateParams.Select(item => item.Id));
                Assert.Same(selected, combo.SelectedItem);
                WaitForBrowser(first);
                Assert.Contains("已保存", Assert.IsType<TextBlock>(dialog.FindName("StatusText")).Text);
                dialog.SetViewMode(false);
                Assert.Equal(new[] { "Flow_D", "Flow_B", "Flow_C", "Flow_A" }, list.Items.Cast<TemplateModel<FlowParam>>().Select(item => item.Key));
            }
            finally { dialog.Close(); }
            var reopened = new FlowTemplateManagerWindow(new BrowserTestFlow(storage), 1, cache) { Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false };
            try
            {
                reopened.Show();
                WaitForBrowser(reopened.OrderLoadTask);
                var list = Assert.IsType<ListView>(reopened.FindName("TemplateList"));
                Assert.Equal(new[] { "Flow_D", "Flow_B", "Flow_C", "Flow_A" }, list.Items.Cast<TemplateModel<FlowParam>>().Select(item => item.Key));
                Assert.Equal(new[] { "Flow_A", "Flow_B", "Flow_C", "Flow_D" }, storage.Load().Select(item => item.Name));
                Assert.Same(selected, combo.SelectedItem);
                var button = new Button { Style = (Style)Application.Current.FindResource("ButtonProperty") };
                Assert.Equal("操作", System.Windows.Automation.AutomationProperties.GetName(button));
            }
            finally { reopened.Close(); }
        });
    }

    private static void WaitForBrowser(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { DrainBrowser(); Thread.Sleep(10); }
        Assert.True(task.IsCompletedSuccessfully, task.Exception?.ToString());
        DrainBrowser();
    }

    private static void DrainBrowser()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static void CaptureBrowser(FrameworkElement visual, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("FLOW_BROWSER_PREVIEW_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        visual.UpdateLayout();
        Directory.CreateDirectory(directory);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name));
        encoder.Save(stream);
    }

    private static IEnumerable<DependencyObject> VisualChildren(DependencyObject root)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in VisualChildren(child)) yield return descendant;
        }
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
            var rcInstanceField = typeof(ColorVision.Engine.Services.RC.MqttRCService).GetField("_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            object? previousRc = rcInstanceField.GetValue(null);
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
                // The isolated workspace may create the RC singleton; stop its keepalive
                // before restoring the previous (possibly absent) configuration service.
                if (previousRc == null && rcInstanceField.GetValue(null) is ColorVision.Engine.Services.RC.MqttRCService createdRc)
                {
                    createdRc.Dispose();
                    rcInstanceField.SetValue(null, null);
                }
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
