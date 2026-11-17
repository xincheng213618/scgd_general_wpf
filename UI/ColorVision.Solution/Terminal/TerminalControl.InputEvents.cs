using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Solution.Terminal
{
    public partial class TerminalControl
    {
        private void ImeProxy_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            ForwardCommittedText(e);

            // Standard keystrokes never enter the proxy's document. IME composition can leave residual text.
            if (e.Handled && ImeProxy.Text.Length != 0)
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, ImeProxy.Clear);
        }

        private void UpdateImeProxyPosition()
        {
            double cursorX = TerminalDisplay.CursorCol * TerminalDisplay.CharWidth;
            double cursorY = TerminalDisplay.CursorY - TerminalDisplay.VerticalOffset;
            double maxX = Math.Max(0, TerminalDisplay.ViewportWidth - ImeProxy.ActualWidth);
            double maxY = Math.Max(0, TerminalDisplay.ViewportHeight - TerminalDisplay.LineHeight);
            Canvas.SetLeft(ImeProxy, Math.Clamp(cursorX, 0, maxX));
            Canvas.SetTop(ImeProxy, Math.Clamp(cursorY, 0, maxY));
        }

        private void TerminalScrollViewer_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            ForwardCommittedText(e);
        }

        private void ForwardCommittedText(TextCompositionEventArgs e)
        {
            if (e.Text.Length != 0)
            {
                SendTerminalInput(e.Text);
                e.Handled = true;
            }
            else if (e.SystemText.Length != 0)
            {
                SendTerminalInput("\u001b" + e.SystemText);
                e.Handled = true;
            }
        }

        private void OutputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            HandleTerminalKey(e);
        }

        private void TerminalScrollViewer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            HandleTerminalKey(e);
            FocusTerminalInput();
        }

        private void HandleTerminalKey(KeyEventArgs e)
        {
            // WPF and the IME consume composition keys before the terminal sees committed text.
            if (e.Key is Key.ImeProcessed or Key.DeadCharProcessed)
                return;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            ModifierKeys modifiers = e.KeyboardDevice.Modifiers;
            const ModifierKeys controlShift = ModifierKeys.Control | ModifierKeys.Shift;

            if (key == Key.C && modifiers is ModifierKeys.Control or controlShift)
            {
                if (modifiers == controlShift || TerminalDisplay.HasSelection)
                    CopySelectedText();
                else
                    SendCtrlC();
                e.Handled = true;
                return;
            }

            if ((key == Key.V && modifiers is ModifierKeys.Control or controlShift) ||
                (key == Key.Insert && modifiers == ModifierKeys.Shift))
            {
                e.Handled = true;
                if (!e.IsRepeat)
                    PasteClipboardText();
                return;
            }

            if (modifiers == controlShift && ScrollTerminalHistory(key))
            {
                e.Handled = true;
                return;
            }

            string? sequence = EncodeTerminalKey(key, modifiers, _screenBuffer.ApplicationCursorKeys, _screenBuffer.Win32InputMode);
            if (sequence != null)
            {
                SendTerminalInput(sequence);
                e.Handled = true;
            }
        }

        private bool ScrollTerminalHistory(Key key)
        {
            switch (key)
            {
                case Key.PageUp:
                    TerminalScrollViewer.PageUp();
                    return true;
                case Key.PageDown:
                    TerminalScrollViewer.PageDown();
                    return true;
                case Key.Home:
                    TerminalScrollViewer.ScrollToTop();
                    return true;
                case Key.End:
                    TerminalScrollViewer.ScrollToBottom();
                    return true;
                default:
                    return false;
            }
        }

        private void TerminalScrollViewer_GotFocus(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(e.OriginalSource, TerminalScrollViewer))
            {
                QueueInitialShellStart();
                FocusTerminalInput();
            }
        }

        private void StartShellFromMenu(string shell)
        {
            _currentShell = shell;
            StartShell();
            FocusTerminalInput(force: true);
        }

        private void MenuNewPowerShell_Click(object sender, RoutedEventArgs e)
        {
            StartShellFromMenu("powershell");
            e.Handled = true;
        }

        private void MenuNewCmd_Click(object sender, RoutedEventArgs e)
        {
            StartShellFromMenu("cmd");
            e.Handled = true;
        }

        private void MenuKill_Click(object sender, RoutedEventArgs e)
        {
            KillShell();
            AppendOutput("[终端已终止]\r\n");
            FocusTerminalInput(force: true);
            e.Handled = true;
        }

        private void OpenActionsMenu(UIElement anchor)
        {
            ContextMenu menu = TerminalRoot.ContextMenu;
            menu.PlacementTarget = anchor;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void TerminalContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            var menu = (ContextMenu)sender;
            menu.ClearValue(ContextMenu.PlacementTargetProperty);
            menu.ClearValue(ContextMenu.PlacementProperty);
        }

        private void TerminalContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            MenuCopy.IsEnabled = TerminalDisplay.HasSelection;
            MenuPaste.IsEnabled = !_disposed && ClipboardHasText();
            UpdateCommandStates();
            UpdateTerminalStatus();
        }

        private void MenuCopy_Click(object sender, RoutedEventArgs e)
        {
            CopySelectedText();
            FocusTerminalInput(force: true);
            e.Handled = true;
        }

        private void MenuPaste_Click(object sender, RoutedEventArgs e)
        {
            PasteClipboardText();
            FocusTerminalInput(force: true);
            e.Handled = true;
        }

        private void MenuClear_Click(object sender, RoutedEventArgs e)
        {
            ClearOutput();
            FocusTerminalInput(force: true);
            e.Handled = true;
        }
    }
}
