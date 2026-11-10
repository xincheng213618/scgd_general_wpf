using AvalonDock;
using AvalonDock.Controls;
using ColorVision.ImageEditor;
using ColorVision.Solution.Editor;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace ColorVision;

/// <summary>Provides the same workspace file drop fallback in docked and floating windows.</summary>
internal sealed class WorkspaceFileDropRouter
{
    private static readonly ConditionalWeakTable<DockingManager, WorkspaceFileDropRouter> Workspaces = new();
    private readonly ConditionalWeakTable<UIElement, Window> _dropOwners = new();
    private readonly Func<string[], Window, Task> _openFiles;

    static WorkspaceFileDropRouter()
    {
        EventManager.RegisterClassHandler(typeof(LayoutFloatingWindowControl), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnFloatingWindowLoaded));
        EventManager.RegisterClassHandler(typeof(DetachedAnchorableWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnFloatingWindowLoaded));
    }

    internal WorkspaceFileDropRouter(Window mainWindow, DockingManager dockingManager, Func<string[], Window, Task>? openFiles = null)
    {
        _openFiles = openFiles ?? OpenWorkspaceFilesAsync;
        Workspaces.Add(dockingManager, this);
        Attach(mainWindow, mainWindow);
        mainWindow.Closed += (_, _) => Workspaces.Remove(dockingManager);
    }

    private static void OnFloatingWindowLoaded(object sender, RoutedEventArgs e)
    {
        var manager = sender switch
        {
            LayoutFloatingWindowControl floating => floating.Model.Root?.Manager,
            DetachedAnchorableWindow detached => detached.Model.Root?.Manager,
            _ => null,
        };
        if (manager is not DockingManager dockingManager || !Workspaces.TryGetValue(dockingManager, out var router))
            return;

        var window = (Window)sender;
        router.Attach(window, window);
        // AvalonDock places the dockable content in a child HWND with its own WPF root.
        if (window.Content is HwndHost host && host.Handle != IntPtr.Zero && HwndSource.FromHwnd(host.Handle)?.RootVisual is UIElement contentRoot)
            router.Attach(contentRoot, window);
    }

    private void Attach(UIElement target, Window owner)
    {
        if (_dropOwners.TryGetValue(target, out _)) return;

        _dropOwners.Add(target, owner);
        target.AllowDrop = true;
        ImageView.SetForwardUnsupportedFileDrops(target, true);
        target.Drop += OnDrop;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } paths
            || sender is not UIElement target || !_dropOwners.TryGetValue(target, out var owner))
            return;

        e.Handled = true;
        await _openFiles(paths, owner);
    }

    private static Task OpenWorkspaceFilesAsync(string[] paths, Window owner)
        => ResourceOpenService.Instance.TryOpenManyWithFeedbackAsync(paths, owner);
}
