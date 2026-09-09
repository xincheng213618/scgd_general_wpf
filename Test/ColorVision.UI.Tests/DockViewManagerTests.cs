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
            configHandler.IsAutoSave = false;
            DisPlayManagerConfig.Instance.LastSelectIndex = 1;

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
            int selectedIndex = config.LastSelectIndex;
            var manager = DisPlayManager.GetInstance();
            try
            {
                manager.IDisPlayControls.Clear();
                config.StoreIndex = new() { ["A"] = 0, ["B"] = 1, ["C"] = 2 };
                config.Groups = new();
                config.ControlGroups = new();
                config.PinnedControls = new();
                config.LastSelectIndex = 0;
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
                config.LastSelectIndex = selectedIndex;
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

    private sealed class TestDisplayControl(string displayName) : UserControl, IDisPlayControl
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

        public string DisPlayName { get; } = displayName;
    }
}
