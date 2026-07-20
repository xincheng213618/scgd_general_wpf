using AvalonDock.Layout;
using ColorVision.Solution.Workspace;
using ColorVision.UI.Views;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public class DockViewManagerTests
{
    [Fact]
    public void LateRegisteredView_IsAddedToDocumentPaneAndCanBeSelected()
    {
        RunOnStaThread(() =>
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

            displayControl.IsSelected = true;

            Assert.Equal(0, pane.SelectedContentIndex);
            Assert.True(document.IsActive);
            Assert.Same(view, manager.LastActiveView);

            manager.SetViewTitle(view, "Renamed Camera");
            Assert.Equal("Renamed Camera", document.Title);

            DockViewManagerHost.ClearViewDocuments();
            ResetDockViewManager(manager);
        });
    }

    [Fact]
    public void ReplaceControls_RestoresLastSelectedIndexAfterDeferredCreation()
    {
        RunOnStaThread(() =>
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
                var first = new TestDisplayControl("First");
                var second = new TestDisplayControl("Second");

                manager.ReplaceControls(new IDisPlayControl[] { first, second });

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

    private static void ResetDockViewManager(DockViewManager manager)
    {
        manager.Views.Clear();
        manager.ViewTitles.Clear();
        manager.LastActiveView = null;
        manager.ActiveViewHandler = null;
        manager.SelectViewHandler = null;
        manager.ViewAddedHandler = null;
        manager.ViewTitleChangedHandler = null;
        manager.ShowAllViewsHandler = null;
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
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
