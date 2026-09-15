using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Themes.Controls
{
    /// <summary>Displays a themed message box on the owner's or application's UI thread.</summary>
    public sealed class MessageBox1
    {
        private static MessageBoxResult Initialize(Window? owner, string messageBoxText, string? caption = null,
            MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None,
            MessageBoxResult defaultResult = MessageBoxResult.None, MessageBoxOptions options = MessageBoxOptions.None)
        {
            const MessageBoxOptions supported = MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading |
                MessageBoxOptions.ServiceNotification | MessageBoxOptions.DefaultDesktopOnly;
            if ((options & ~supported) != 0)
                throw new InvalidEnumArgumentException(nameof(options), (int)options, typeof(MessageBoxOptions));

            caption ??= Properties.Resources.MsgBox_Prompt;
            // Desktop/service flags require a native message box; pass every option through unchanged.
            if ((options & (MessageBoxOptions.ServiceNotification | MessageBoxOptions.DefaultDesktopOnly)) != 0)
                return owner == null
                    ? System.Windows.MessageBox.Show(messageBoxText, caption, button, icon, defaultResult, options)
                    : System.Windows.MessageBox.Show(owner, messageBoxText, caption, button, icon, defaultResult, options);

            return Invoke(owner, () =>
            {
                var dialog = CreateDialog(owner, messageBoxText, caption, button, icon, defaultResult);
                if ((options & MessageBoxOptions.RtlReading) != 0) dialog.FlowDirection = FlowDirection.RightToLeft;
                if ((options & MessageBoxOptions.RightAlign) != 0) dialog.messageBoxText.TextAlignment = TextAlignment.Right;
                dialog.ShowDialog();
                return dialog.MessageBoxResult;
            });
        }

        private static T Invoke<T>(Window? owner, Func<T> action)
        {
            Dispatcher dispatcher = owner?.Dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            return dispatcher.CheckAccess() ? action() : dispatcher.Invoke(action);
        }

        private static MessageBoxWindow CreateDialog(Window? owner, string messageBoxText, string caption,
            MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        {
            if (owner == null && Application.Current is { } app)
            {
                foreach (Window window in app.Windows)
                {
                    if (window.Dispatcher.CheckAccess() && window.IsVisible && window.IsActive)
                    {
                        owner = window;
                        break;
                    }
                }
                if (owner == null && app.MainWindow is { } main && main.Dispatcher.CheckAccess() && main.IsVisible)
                    owner = main;
            }

            return new MessageBoxWindow(messageBoxText, caption, button, icon, defaultResult)
            {
                Owner = owner,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                Topmost = owner?.Topmost ?? false
            };
        }

        /// <summary>Shows an optional reminder and returns the user's "don't show again" choice.</summary>
        public static bool ShowAgain(string messageBoxText, string caption, bool DontShowAgain)
        {
            if (DontShowAgain) return true;
            return Invoke(null, () =>
            {
                var dialog = CreateDialog(null, messageBoxText, caption ?? Properties.Resources.MsgBox_Prompt,
                    MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None);
                dialog.show_again.Visibility = Visibility.Visible;
                dialog.ShowDialog();
                return dialog.DontShowAgain;
            });
        }

        public static MessageBoxResult Show(string messageBoxText) => Initialize(null, messageBoxText);
        public static MessageBoxResult Show(string messageBoxText, string caption) => Initialize(null, messageBoxText, caption);
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button) => Initialize(null, messageBoxText, caption, button);
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon) => Initialize(null, messageBoxText, caption, button, icon);
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult) => Initialize(null, messageBoxText, caption, button, icon, defaultResult);
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options) => Initialize(null, messageBoxText, caption, button, icon, defaultResult, options);

        public static MessageBoxResult Show(Window? owner, string messageBoxText) => Initialize(owner, messageBoxText);
        public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption) => Initialize(owner, messageBoxText, caption);
        public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button) => Initialize(owner, messageBoxText, caption, button);
        public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon) => Initialize(owner, messageBoxText, caption, button, icon);
        public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult) => Initialize(owner, messageBoxText, caption, button, icon, defaultResult);
        public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options) => Initialize(owner, messageBoxText, caption, button, icon, defaultResult, options);
    }
}
