using AvalonDock.Layout;
using ColorVision.Solution.Workspace;
using ColorVision.UI.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

public class DockViewManagerTests
{
    [Fact]
    public void LateRegisteredView_IsAddedToDocumentPaneAndTitleCanBeUpdated()
    {
        WpfTestHost.Invoke(() =>
        {
            DockViewManager manager = DockViewManager.GetInstance();
            ResetDockViewManager(manager);
            var pane = new LayoutDocumentPane();
            WorkspaceManager.LayoutDocumentPane = pane;
            DockViewManagerHost.ClearViewDocuments();
            DockViewManagerHost.Initialize();

            var view = new UserControl();
            var displayControl = new TestDisplayControl("Camera");
            displayControl.AddViewConfig(view, "Camera");

            LayoutDocument document = Assert.IsType<LayoutDocument>(Assert.Single(pane.Children));
            Assert.Equal("Camera", document.Title);
            Assert.Same(view, document.Content);

            manager.SetViewTitle(view, "Renamed Camera");
            Assert.Equal("Renamed Camera", document.Title);

            DockViewManagerHost.ClearViewDocuments();
            ResetDockViewManager(manager);
        });
    }

    [Fact]
    public void DoubleClickingDisplayControl_ActivatesAssociatedView()
    {
        WpfTestHost.Invoke(() =>
        {
            DockViewManager viewManager = DockViewManager.GetInstance();
            ResetDockViewManager(viewManager);
            var pane = new LayoutDocumentPane();
            WorkspaceManager.LayoutDocumentPane = pane;
            DockViewManagerHost.ClearViewDocuments();
            DockViewManagerHost.Initialize();

            var view = new UserControl();
            var displayControl = new TestDisplayControl("Camera");
            displayControl.AddViewConfig(view, "Camera");

            var otherView = new UserControl();
            var otherDisplayControl = new TestDisplayControl("Other");
            otherDisplayControl.AddViewConfig(otherView, "Other");
            viewManager.SelectView(otherView);
            DisPlayManager displayManager = DisPlayManager.GetInstance();
            displayManager.SelectControl(otherDisplayControl);
            DisPlayManagerExtension.RegisterSelectionInput(displayControl, displayControl);

            LayoutDocument document = Assert.IsType<LayoutDocument>(pane.Children[0]);
            LayoutDocument otherDocument = Assert.IsType<LayoutDocument>(pane.Children[1]);
            Assert.False(document.IsActive);
            Assert.Equal(1, pane.SelectedContentIndex);

            displayControl.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = Mouse.PreviewMouseDownEvent
            });

            Assert.Equal(1, pane.SelectedContentIndex);
            Assert.Same(otherView, viewManager.LastActiveView);
            Assert.Same(displayControl, displayManager.SelectedControl);

            displayControl.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = Control.MouseDoubleClickEvent
            });

            Assert.True(document.IsActive);
            Assert.False(otherDocument.IsActive);
            Assert.Equal(0, pane.SelectedContentIndex);
            Assert.Same(view, viewManager.LastActiveView);

            DockViewManagerHost.ClearViewDocuments();
            ResetDockViewManager(viewManager);
        });
    }

    [Fact]
    public void RemoveView_ClearsManagerStateAndHostedDocument()
    {
        WpfTestHost.Invoke(() =>
        {
            DockViewManager manager = DockViewManager.GetInstance();
            ResetDockViewManager(manager);
            var pane = new LayoutDocumentPane();
            WorkspaceManager.LayoutDocumentPane = pane;
            DockViewManagerHost.ClearViewDocuments();
            DockViewManagerHost.Initialize();

            var view = new UserControl();
            manager.SetViewTitle(view, "Camera");
            manager.AddView(view);
            manager.ActiveView(view);
            LayoutDocument document = Assert.IsType<LayoutDocument>(Assert.Single(pane.Children));

            manager.RemoveView(view);

            Assert.DoesNotContain(view, manager.Views);
            Assert.False(manager.ViewTitles.ContainsKey(view));
            Assert.Null(manager.LastActiveView);
            Assert.Empty(pane.Children);
            Assert.Null(document.Content);

            DockViewManagerHost.ClearViewDocuments();
            ResetDockViewManager(manager);
        });
    }

    [Fact]
    public void ReplaceControls_RestoresLastSelectedIndexAfterDeferredCreation()
    {
        WpfTestHost.Invoke(() =>
        {
            ConfigHandler configHandler = ConfigHandler.GetInstance("ColorVisionUITests");
            bool originalAutoSave = configHandler.IsAutoSave;
            int originalIndex = DisPlayManagerConfig.Instance.LastSelectIndex;
            string originalKey = DisPlayManagerConfig.Instance.LastSelectedControlKey;
            configHandler.IsAutoSave = false;
            DisPlayManagerConfig.Instance.LastSelectIndex = 1;
            DisPlayManagerConfig.Instance.LastSelectedControlKey = string.Empty;

            DisPlayManager manager = DisPlayManager.GetInstance();
            try
            {
                manager.IDisPlayControls.Clear();
                var panel = new StackPanel();
                manager.Init(new Window(), panel);
                var first = new TestDisplayControl("First") { Margin = new Thickness(3, 8, 4, 9) };
                var second = new TestDisplayControl("Second");

                manager.ReplaceControls(new IDisPlayControl[] { first, second });

                Assert.Equal(new Thickness(3, 0, 4, 2), first.Margin);
                Assert.Equal(new Thickness(0, 0, 0, 2), second.Margin);
                Assert.Equal(2, panel.Children.Count);
                Assert.DoesNotContain(panel.Children.OfType<Border>(), border =>
                    border.Child is DockPanel dockPanel && dockPanel.Children.OfType<Button>().Any(button => Equals(button.Content, "+ 分组")));
                Assert.False(first.IsSelected);
                Assert.True(second.IsSelected);
                Assert.Same(second, manager.SelectedControl);
                Assert.Equal(1, DisPlayManagerConfig.Instance.LastSelectIndex);
            }
            finally
            {
                manager.IDisPlayControls.Clear();
                DisPlayManagerConfig.Instance.LastSelectIndex = originalIndex;
                DisPlayManagerConfig.Instance.LastSelectedControlKey = originalKey;
                configHandler.IsAutoSave = originalAutoSave;
            }
        });
    }

    [Fact]
    public void Pinning_PreservesBaseOrderSelectionAndReloadedConfiguration()
    {
        WithDisplayOrder((manager, config, controls) =>
        {
            manager.SelectControl(controls[1]);
            manager.SetPinned(controls[2], true);
            Assert.Equal(new[] { "C", "A", "B" }, manager.IDisPlayControls.Select(a => a.DisPlayName));
            Assert.Same(controls[1], manager.SelectedControl);
            Assert.Equal(2, config.LastSelectIndex);
            Assert.Equal(2, config.StoreIndex["C"]);

            var saved = Newtonsoft.Json.JsonConvert.DeserializeObject<DisPlayManagerConfig>(
                Newtonsoft.Json.JsonConvert.SerializeObject(config))!;
            config.PinnedControls = saved.PinnedControls;
            config.StoreIndex = saved.StoreIndex;
            manager.ReplaceControls(controls);
            Assert.Equal(new[] { "C", "A", "B" }, manager.IDisPlayControls.Select(a => a.DisPlayName));
            Assert.Equal(2, config.StoreIndex["C"]);
            manager.SetPinned(controls[2], false);
            Assert.Equal(new[] { "A", "B", "C" }, manager.IDisPlayControls.Select(a => a.DisPlayName));
            Assert.Same(controls[1], manager.SelectedControl);
        });
    }

    [Fact]
    public void PinnedRows_RespectGroupsAndDraggingDoesNotBakePinOrderIntoBaseOrder()
    {
        WithDisplayOrder((manager, config, controls) =>
        {
            manager.SetPinned(controls[2], true);
            var move = typeof(DisPlayManager).GetMethod("MoveControlToGroup",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            move.Invoke(manager, new object[] { "B", DisPlayManagerConfig.DefaultGroupId, 1 });
            Assert.Equal(new[] { "C", "B", "A" }, manager.IDisPlayControls.Select(a => a.DisPlayName));
            manager.SetPinned(controls[2], false);
            Assert.Equal(new[] { "B", "A", "C" }, manager.IDisPlayControls.Select(a => a.DisPlayName));

            config.Groups.Add(new DisPlayGroupConfig { Id = "other", Name = "Other" });
            manager.SetPinned(controls[2], true);
            move.Invoke(manager, new object[] { "C", "other", 0 });
            Assert.Equal(new[] { "B", "A", "C" }, manager.IDisPlayControls.Select(a => a.DisPlayName));
            Assert.True(DisPlayManager.IsPinned(controls[2]));
            Assert.Equal("other", config.ControlGroups["C"]);
        });
    }

    [Fact]
    public void PinButton_ClickPinsOwnerWithoutCollapsingHeaderOrOpeningDetails()
    {
        WithDisplayOrder((manager, config, controls) =>
        {
            var pin = new DisplayPinButton();
            var header = new System.Windows.Controls.Primitives.ToggleButton { IsChecked = true };
            var panel = new DockPanel();
            panel.Children.Add(pin);
            panel.Children.Add(header);
            controls[2].Content = panel;
            var viewManager = DockViewManager.GetInstance();
            var previous = viewManager.LastActiveView;
            var details = new UserControl();
            controls[2].AddViewConfig(details, "C");
            try
            {
                pin.IsChecked = true;
                pin.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.True(DisPlayManager.IsPinned(controls[2]));
                Assert.True(header.IsChecked);
                Assert.True(DisplayPinButton.IsPinInput(pin));
                pin.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = Control.MouseDoubleClickEvent
                });
                Assert.Same(previous, viewManager.LastActiveView);
            }
            finally
            {
                viewManager.RemoveView(details);
            }
        });
    }

    [Fact]
    public void CentralState_MigratesLegacyNameThenSurvivesDisplayNameChange()
    {
        WithDisplayOrder((manager, config, controls) =>
        {
            var legacy = new TestDisplayControl("旧名称", "device-code", withHeader: true);
            config.StoreIndex[legacy.DisPlayName] = 0;
            config.PinnedControls.Add(legacy.DisPlayName);
            config.ControlGroups[legacy.DisPlayName] = DisPlayManagerConfig.DefaultGroupId;
            config.ControlExpandedStates[legacy.DisPlayName] = false;
            config.LastSelectedControlKey = legacy.DisPlayName;

            manager.ReplaceControls(new[] { legacy });

            Assert.False(legacy.Header!.IsChecked);
            Assert.False(config.ControlExpandedStates["device-code"]);
            Assert.True(config.PinnedControls.Contains("device-code"));
            Assert.Equal(0, config.StoreIndex["device-code"]);
            Assert.Equal("device-code", config.LastSelectedControlKey);

            var renamed = new TestDisplayControl("新名称", "device-code", withHeader: true);
            manager.ReplaceControls(new[] { renamed });

            Assert.False(renamed.Header!.IsChecked);
            Assert.True(DisPlayManager.IsPinned(renamed));
            Assert.Same(renamed, manager.SelectedControl);
        });
    }

    [Fact]
    public void VisibilityManagement_HidesOnlyPanelRowAndRestoresItsSavedOrder()
    {
        WithDisplayOrder((manager, config, controls) =>
        {
            manager.SelectControl(controls[1]);
            manager.SetControlVisible(controls[1], false);

            Assert.Equal(3, manager.IDisPlayControls.Count);
            Assert.Equal(new[] { "A", "C" }, manager.StackPanel.Children.OfType<TestDisplayControl>().Select(control => control.DisPlayName));
            Assert.Contains("B", config.HiddenControls);
            Assert.Same(controls[0], manager.SelectedControl);

            manager.SetControlVisible(controls[1], true);

            Assert.Equal(new[] { "A", "B", "C" }, manager.StackPanel.Children.OfType<TestDisplayControl>().Select(control => control.DisPlayName));
            Assert.DoesNotContain("B", config.HiddenControls);
            Assert.Equal(1, config.StoreIndex["B"]);
        });
    }

    [Fact]
    public void ManagementWindow_KeepsHiddenControlsAvailableForRecovery()
    {
        WithDisplayOrder((manager, config, controls) =>
        {
            manager.SetControlVisible(controls[1], false);
            var window = new DisplayControlManagerWindow(manager);
            var grid = Assert.IsType<DataGrid>(window.FindName("ControlsDataGrid"));

            Assert.Equal(3, window.Controls.Count);
            Assert.False(Assert.Single(window.Controls, item => item.Name == "B").IsVisible);
            Assert.Equal(new[] { "名称", "分组", "显示", "置顶", "展开", "顺序" }, grid.Columns.Select(column => column.Header));
        });
    }

    private static void WithDisplayOrder(Action<DisPlayManager, DisPlayManagerConfig, TestDisplayControl[]> test)
    {
        WpfTestHost.Invoke(() =>
        {
            var configHandler = ConfigHandler.GetInstance("ColorVisionUITests");
            bool autoSave = configHandler.IsAutoSave;
            configHandler.IsAutoSave = false;
            var config = DisPlayManagerConfig.Instance;
            var indexes = config.StoreIndex;
            var groups = config.Groups;
            var controlGroups = config.ControlGroups;
            var pins = config.PinnedControls;
            var hidden = config.HiddenControls;
            var expanded = config.ControlExpandedStates;
            int selectedIndex = config.LastSelectIndex;
            string selectedKey = config.LastSelectedControlKey;
            var manager = DisPlayManager.GetInstance();
            try
            {
                manager.IDisPlayControls.Clear();
                config.StoreIndex = new() { ["A"] = 0, ["B"] = 1, ["C"] = 2 };
                config.Groups = new();
                config.ControlGroups = new();
                config.PinnedControls = new();
                config.HiddenControls = new();
                config.ControlExpandedStates = new();
                config.LastSelectIndex = 0;
                config.LastSelectedControlKey = string.Empty;
                var controls = new[] { new TestDisplayControl("A"), new TestDisplayControl("B"), new TestDisplayControl("C") };
                manager.Init(new Window(), new StackPanel());
                manager.ReplaceControls(controls);
                test(manager, config, controls);
            }
            finally
            {
                manager.IDisPlayControls.Clear();
                config.StoreIndex = indexes;
                config.Groups = groups;
                config.ControlGroups = controlGroups;
                config.PinnedControls = pins;
                config.HiddenControls = hidden;
                config.ControlExpandedStates = expanded;
                config.LastSelectIndex = selectedIndex;
                config.LastSelectedControlKey = selectedKey;
                configHandler.IsAutoSave = autoSave;
            }
        });
    }

    private static void ResetDockViewManager(DockViewManager manager)
    {
        manager.Views.Clear();
        manager.ViewTitles.Clear();
        manager.LastActiveView = null;
        manager.ActiveViewHandler = null;
        manager.SelectViewHandler = null;
        manager.ViewAddedHandler = null;
        manager.ViewRemovedHandler = null;
        manager.ViewTitleChangedHandler = null;
        manager.ShowAllViewsHandler = null;
    }

    private sealed class TestDisplayControl : UserControl, IDisPlayControl
    {
        public event RoutedEventHandler? Selected;
        public event RoutedEventHandler? Unselected;
        public event EventHandler? SelectChanged;

        public bool IsSelected
        {
            get => field;
            set
            {
                if (field == value)
                    return;
                field = value;
                SelectChanged?.Invoke(this, EventArgs.Empty);
                if (value)
                    Selected?.Invoke(this, new RoutedEventArgs());
                else
                    Unselected?.Invoke(this, new RoutedEventArgs());
            }
        }

        public string DisPlayName { get; }
        public string PersistenceKey { get; }
        public System.Windows.Controls.Primitives.ToggleButton? Header { get; }

        public TestDisplayControl(
            string displayName,
            string? persistenceKey = null,
            bool withHeader = false)
        {
            DisPlayName = displayName;
            PersistenceKey = persistenceKey ?? displayName;
            if (!withHeader)
                return;

            Header = new System.Windows.Controls.Primitives.ToggleButton
            {
                Name = "DisplayHeaderToggle",
                IsChecked = true
            };
            NameScope.SetNameScope(this, new NameScope());
            RegisterName(Header.Name, Header);
            Content = Header;
        }
    }
}
