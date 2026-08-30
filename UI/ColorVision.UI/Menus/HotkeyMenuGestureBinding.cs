using ColorVision.UI.HotKey;
using log4net;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Menus;

/// <summary>Mirrors a contributed hotkey's current combination without registering or invoking it.</summary>
internal static class HotkeyMenuGestureBinding
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(HotkeyMenuGestureBinding));
    private static readonly DependencyProperty SubscriptionProperty = DependencyProperty.RegisterAttached(
        "Subscription", typeof(Subscription), typeof(HotkeyMenuGestureBinding), new PropertyMetadata(null));

    internal static void Attach(MenuItem menuItem, IMenuItem source, ObservableCollection<HotKeys> hotkeys)
    {
        (menuItem.GetValue(SubscriptionProperty) as Subscription)?.Dispose();
        menuItem.ClearValue(SubscriptionProperty);
        if (source is not IHotKey provider) return;

        // A missing runtime definition is not evidence that the declared default is registered.
        menuItem.InputGestureText = string.Empty;
        string id;
        try
        {
            HotKeys declaration = provider.HotKeys;
            if (declaration == null) return;
            if (!string.IsNullOrWhiteSpace(declaration.Id)) id = declaration.Id;
            else
            {
                // Modern multi-action discovery takes precedence over the legacy interface.
                // Without an explicit action ID, a menu cannot identify one of those actions.
                if (source is IHotkeyProvider) return;
                id = source.GetType().FullName ?? source.GetType().Name;
            }
        }
        catch (Exception exception)
        {
            Log.Warn($"Read menu hotkey identity failed: {source.GetType().FullName}: {exception.Message}");
            return;
        }

        var subscription = new Subscription(menuItem, hotkeys, id);
        menuItem.SetValue(SubscriptionProperty, subscription);
        subscription.Connect();
    }

    private sealed class Subscription : IDisposable
    {
        private readonly WeakReference<MenuItem> _menuItem;
        private readonly ObservableCollection<HotKeys> _hotkeys;
        private readonly string _id;
        private HotKeys? _current;
        private bool _disposed;

        internal Subscription(MenuItem menuItem, ObservableCollection<HotKeys> hotkeys, string id)
        {
            _menuItem = new(menuItem);
            _hotkeys = hotkeys;
            _id = id;
        }

        internal void Connect()
        {
            CollectionChangedEventManager.AddHandler(_hotkeys, OnCollectionChanged);
            Refresh();
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();
        private void OnHotkeyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

        private void Refresh()
        {
            if (_disposed) return;
            if (!_menuItem.TryGetTarget(out MenuItem? menuItem)) { Dispose(); return; }
            if (!menuItem.Dispatcher.CheckAccess())
            {
                if (!menuItem.Dispatcher.HasShutdownStarted)
                    menuItem.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(Refresh));
                return;
            }

            HotKeys? current = _hotkeys.FirstOrDefault(item => string.Equals(item.Id, _id, StringComparison.OrdinalIgnoreCase));
            if (!ReferenceEquals(_current, current))
            {
                if (_current != null) PropertyChangedEventManager.RemoveHandler(_current, OnHotkeyChanged, nameof(HotKeys.Hotkey));
                _current = current;
                if (_current != null) PropertyChangedEventManager.AddHandler(_current, OnHotkeyChanged, nameof(HotKeys.Hotkey));
            }
            menuItem.InputGestureText = _current == null || _current.Hotkey.IsEmpty ? string.Empty : HotkeyInput.Format(_current.Hotkey);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CollectionChangedEventManager.RemoveHandler(_hotkeys, OnCollectionChanged);
            if (_current != null) PropertyChangedEventManager.RemoveHandler(_current, OnHotkeyChanged, nameof(HotKeys.Hotkey));
            _current = null;
        }
    }
}
