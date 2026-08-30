using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.HotKey;

/// <summary>
/// Prevents WPF's built-in command gestures from bypassing edited/cleared application shortcuts.
/// Only the supplied host is affected; standalone editor windows retain their native commands.
/// </summary>
public sealed class RoutedCommandHotkeyGuard : IDisposable
{
    private readonly Window _window;
    private readonly HotkeyService _service;
    private readonly List<Hotkey> _nativeGestures;

    public RoutedCommandHotkeyGuard(Window window, HotkeyService service, IEnumerable<RoutedCommand> commands)
    {
        _window = window;
        _service = service;
        _nativeGestures = commands.SelectMany(command => command.InputGestures.OfType<KeyGesture>())
            .Select(gesture => new Hotkey(gesture.Key, gesture.Modifiers))
            // Some editors implement the default directly in PreviewKeyDown instead of
            // using a RoutedCommand (for example the 3D viewer's Ctrl+Shift+S snapshot).
            .Concat(service.HotKeys.SelectMany(action => action.GetDefaultBindings()))
            .Distinct().ToList();
        window.PreviewKeyDown += OnPreviewKeyDown;
        window.Closed += OnClosed;
    }

    internal bool ShouldSuppress(Key key, ModifierKeys modifiers)
    {
        var pressed = new Hotkey(key, modifiers);
        return _nativeGestures.Contains(pressed) && (HotkeyDispatchGate.HasPendingKeyRelease || !_service.HotKeys.Any(action =>
            !action.IsGlobal && action.IsRegistered &&
            (ReferenceEquals(action.Control, _window) ||
             (action.Control?.IsKeyboardFocusWithin == true && Window.GetWindow(action.Control) == _window)) &&
            action.GetBindings().Contains(pressed)));
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (HotkeyDispatchGate.IsSuspended) return;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        ModifierKeys modifiers = Keyboard.Modifiers;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) modifiers |= ModifierKeys.Windows;
        if (ShouldSuppress(key, modifiers)) e.Handled = true;
    }

    private void OnClosed(object? sender, EventArgs e) => Dispose();

    public void Dispose()
    {
        _window.PreviewKeyDown -= OnPreviewKeyDown;
        _window.Closed -= OnClosed;
    }
}
