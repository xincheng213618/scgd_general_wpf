using System;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Themes;

public static class WindowKeyboardNavigation
{
    public static void Attach(Window window, UIElement initialFocus, Action? escapeAction = null)
    {
        KeyboardNavigation.SetTabNavigation(window, KeyboardNavigationMode.Cycle);
        window.ContentRendered += FocusInitialControl;
        window.KeyDown += (_, e) =>
        {
            // Bubbling lets a search field, open combo box or context menu consume Escape first.
            if (e.Handled || e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None)
                return;

            e.Handled = true;
            if (escapeAction != null)
                escapeAction();
            else
                window.Close();
        };

        void FocusInitialControl(object? sender, EventArgs e)
        {
            window.ContentRendered -= FocusInitialControl;
            if (window.IsActive && initialFocus.IsVisible && initialFocus.IsEnabled)
            {
                if (!initialFocus.Focus())
                    initialFocus.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }
        }
    }
}
