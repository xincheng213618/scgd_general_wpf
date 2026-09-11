using System.Windows;
using System.Windows.Input;

namespace ColorVision.Themes;

/// <summary>Input behaviors used by the legacy editor styles, independent of their visual templates.</summary>
public static class InputKeyboardNavigation
{
    public static readonly DependencyProperty EnterMovesFocusProperty = DependencyProperty.RegisterAttached(
        "EnterMovesFocus", typeof(bool), typeof(InputKeyboardNavigation), new PropertyMetadata(false, OnEnterMovesFocusChanged));
    public static bool GetEnterMovesFocus(DependencyObject element) => (bool)element.GetValue(EnterMovesFocusProperty);
    public static void SetEnterMovesFocus(DependencyObject element, bool value) => element.SetValue(EnterMovesFocusProperty, value);
    private static void OnEnterMovesFocusChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not UIElement input) return;
        input.PreviewKeyDown -= MoveFocusOnEnter;
        if ((bool)e.NewValue) input.PreviewKeyDown += MoveFocusOnEnter;
    }

    public static readonly DependencyProperty NumberKeysOnlyProperty = DependencyProperty.RegisterAttached(
        "NumberKeysOnly", typeof(bool), typeof(InputKeyboardNavigation), new PropertyMetadata(false, OnNumberKeysOnlyChanged));
    public static bool GetNumberKeysOnly(DependencyObject element) => (bool)element.GetValue(NumberKeysOnlyProperty);
    public static void SetNumberKeysOnly(DependencyObject element, bool value) => element.SetValue(NumberKeysOnlyProperty, value);
    private static void OnNumberKeysOnlyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not UIElement input) return;
        input.PreviewKeyDown -= FilterNumberKeys;
        if ((bool)e.NewValue) input.PreviewKeyDown += FilterNumberKeys;
    }

    internal static void MoveFocusOnEnter(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (sender is UIElement input) input.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        e.Handled = true;
    }

    internal static void FilterNumberKeys(object sender, KeyEventArgs e)
    {
        // Retain the existing numeric editor behavior, including clipboard shortcuts.
        if (e.Key == Key.Enter)
        {
            if (sender is UIElement input) input.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            e.Handled = false;
            return;
        }
        if (e.Key is Key.Back or Key.Left or Key.Right or Key.Tab or Key.Delete or Key.Home or Key.End)
        {
            e.Handled = false;
            return;
        }
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key is Key.V or Key.C or Key.X or Key.A)
        {
            e.Handled = false;
            return;
        }
        e.Handled = !((e.Key >= Key.D0 && e.Key <= Key.D9 && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) ||
            (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) || e.Key is Key.Decimal or Key.OemPeriod);
    }
}
