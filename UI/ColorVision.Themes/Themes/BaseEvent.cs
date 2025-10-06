using System.Windows;
using System.Windows.Input;

namespace ColorVision.Themes
{
    public partial class BaseEvent : ResourceDictionary
    {
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is UIElement uie)
                    uie.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));

                e.Handled = true;
            }
        }

        public void NumberValidationTextBox(object sender, KeyEventArgs e)
        {
            // Allow navigation/editing keys
            if (e.Key == Key.Enter)
            {
                if (sender is UIElement uie)
                    uie.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                e.Handled = false;
                return;
            }

            if (e.Key == Key.Back || e.Key == Key.Left || e.Key == Key.Right ||
                e.Key == Key.Tab || e.Key == Key.Delete ||
                e.Key == Key.Home || e.Key == Key.End)
            {
                e.Handled = false;
                return;
            }

            // Allow Ctrl+V/C/X/A shortcuts
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.V || e.Key == Key.C || e.Key == Key.X || e.Key == Key.A)
                {
                    e.Handled = false;
                    return;
                }
            }

            // Allow digits
            if ((e.Key >= Key.D0 && e.Key <= Key.D9 && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) ||
                (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
            {
                e.Handled = false;
                return;
            }

            // Allow decimal points (regular and numpad)
            if (e.Key == Key.Decimal || e.Key == Key.OemPeriod)
            {
                e.Handled = false;
                return;
            }

            // Block everything else
            e.Handled = true;
        }
    }
}
