using System.Windows.Input;

namespace ColorVision.UI.HotKey;

public static class HotkeyInput
{
    public static bool IsModifier(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    public static bool IsValid(Hotkey key)
    {
        if (key == null || !Enum.IsDefined(key.Key)) return false;
        const ModifierKeys supported = ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Windows;
        if ((key.Modifiers & ~supported) != 0) return false;
        if (key.Key == Key.None) return key.Modifiers == ModifierKeys.None;
        if (IsModifier(key.Key) || key.Key is Key.Clear or Key.OemClear or Key.Apps or Key.System or Key.ImeProcessed or Key.DeadCharProcessed) return false;
        if (key.Modifiers == ModifierKeys.None && key.Key is Key.Enter or Key.Space or Key.Tab or Key.Delete or Key.Back or Key.Escape) return false;
        bool character = key.Key is >= Key.A and <= Key.Z or >= Key.D0 and <= Key.D9 or >= Key.NumPad0 and <= Key.NumPad9
            or Key.OemQuestion or Key.OemQuotes or Key.OemPlus or Key.OemOpenBrackets or Key.OemCloseBrackets or Key.OemMinus
            or Key.OemSemicolon or Key.OemPipe or Key.OemTilde or Key.Oem8 or Key.OemPeriod or Key.OemComma
            or Key.Add or Key.Divide or Key.Multiply or Key.Subtract or Key.Oem102 or Key.Decimal or Key.AbntC1 or Key.AbntC2;
        return !character || key.Modifiers is not (ModifierKeys.None or ModifierKeys.Shift);
    }

    public static string Format(Hotkey key)
    {
        if (key.IsEmpty) return HotkeyEditorText.Unassigned;
        List<string> parts = new();
        if (key.Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        if (key.Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (key.Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (key.Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        parts.Add(key.Key switch
        {
            Key.OemOpenBrackets => "[", Key.OemCloseBrackets => "]", Key.OemPlus => "=", Key.OemMinus => "-",
            Key.OemComma => ",", Key.OemPeriod => ".", Key.OemQuestion => "/", Key.OemSemicolon => ";",
            Key.OemQuotes => "'", Key.OemPipe => "\\", Key.OemTilde => "`", Key.Return => "Enter",
            Key.Back => "Backspace", Key.Escape => "Esc", Key.Prior => "PageUp", Key.Next => "PageDown",
            >= Key.D0 and <= Key.D9 => ((int)key.Key - (int)Key.D0).ToString(),
            _ => key.Key.ToString()
        });
        return string.Join("+", parts);
    }
}
