using ColorVision.UI.HotKey;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Serach;

/// <summary>Refocuses an already-open palette when keyboard events stay inside its Popup HWND.</summary>
public sealed class SearchPopupHotkeyBridge : IDisposable
{
    private readonly Window _owner;
    private readonly UIElement _content;
    private readonly HotkeyService _hotkeys;
    private readonly HashSet<string> _actionIds;
    private readonly Action _focus;

    public SearchPopupHotkeyBridge(Window owner, UIElement content, HotkeyService hotkeys, IEnumerable<string> actionIds, Action focus)
    {
        _owner = owner;
        _content = content;
        _hotkeys = hotkeys;
        _actionIds = actionIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _focus = focus;
        _content.PreviewKeyDown += OnPreviewKeyDown;
        _content.AddHandler(Keyboard.PreviewKeyUpEvent, new KeyEventHandler(OnPreviewKeyUp), handledEventsToo: true);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || !_owner.IsActive) return;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        ModifierKeys modifiers = Keyboard.Modifiers;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) modifiers |= ModifierKeys.Windows;
        if (TryRefocus(key, modifiers, e.IsRepeat)) e.Handled = true;
    }

    internal bool TryRefocus(Key key, ModifierKeys modifiers, bool isRepeat)
    {
        if (key is Key.ImeProcessed or Key.DeadCharProcessed or Key.None || HotkeyDispatchGate.IsSuspended) return false;
        Hotkey gesture = new(key, modifiers);
        if (!_hotkeys.HotKeys.Any(action => _actionIds.Contains(action.Id) && action.IsRegistered && !action.IsGlobal
            && ReferenceEquals(action.Control, _owner) && action.GetBindings().Contains(gesture))) return false;
        if (!HotkeyDispatchGate.ShouldSuppress(key) && !isRepeat) _focus();
        return true;
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
        => HotkeyDispatchGate.ShouldSuppress(e.Key == Key.System ? e.SystemKey : e.Key, isKeyUp: true);

    public void Dispose()
    {
        _content.PreviewKeyDown -= OnPreviewKeyDown;
        _content.RemoveHandler(Keyboard.PreviewKeyUpEvent, new KeyEventHandler(OnPreviewKeyUp));
    }
}
