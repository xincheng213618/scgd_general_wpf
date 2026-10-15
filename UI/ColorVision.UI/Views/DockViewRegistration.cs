using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Views;

/// <summary>A view factory that owns only the current document instance.</summary>
public sealed class DockViewRegistration : IDisposable
{
    private readonly Func<Control> _factory;
    private readonly Dispatcher _dispatcher;
    private Control? _view;
    private bool _disposed;

    public DockViewRegistration(Func<Control> factory, string title)
    {
        _factory = factory;
        Title = title;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public string Title { get; set; }
    public Control? Current => Volatile.Read(ref _view);
    public event Action<Control>? ViewCreated;
    public event Action<Control>? ViewReleasing;

    public Control GetOrCreate()
    {
        if (!_dispatcher.CheckAccess()) return _dispatcher.Invoke(GetOrCreate);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_view != null) return _view;
        DockViewManager.GetInstance().RegisterView(this);
        _view = _factory();
        ViewCreated?.Invoke(_view);
        return _view;
    }

    internal void Release()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(Release);
            return;
        }
        Control? previous = _view;
        _view = null;
        if (previous == null) return;
        DockViewManager.GetInstance().RemoveView(previous);
        try { ViewReleasing?.Invoke(previous); }
        finally { (previous as IDisposable)?.Dispose(); }
    }

    public void Dispose()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(Dispose);
            return;
        }
        if (_disposed) return;
        _disposed = true;
        DockViewManager.GetInstance().UnregisterView(this);
        Release();
        ViewCreated = null;
        ViewReleasing = null;
    }
}
