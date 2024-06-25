using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Themes
{
    //
    // 摘要:
    //     Displays a message box.
    public sealed class MessageBox1
    {
        //icon 的部分建议移除，目前采用了已经淘汰的system.drawing.Common,如果放在其他地方，需要重置图像
        //参考了一些代码，并不是显示上的最优解，这里可以做一些调整

        private static MessageBoxResult Initialize(string messageBoxText, string caption ="提示", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            MessageBoxResult MessageBoxResult = MessageBoxResult.None;
            Application.Current.Dispatcher.Invoke(delegate
            {
                Controls.MessageBoxWindow messageBox1 = new Controls.MessageBoxWindow(messageBoxText, caption, button, icon, defaultResult);
                messageBox1.Topmost = true;
                messageBox1.ShowDialog();
                MessageBoxResult = messageBox1.MessageBoxResult;
            });
            return MessageBoxResult;
        }

        public static MessageBoxResult Show(string messageBoxText)
        {
            return Initialize(messageBoxText);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption)
        {
            return Initialize(messageBoxText, caption);
        }

        /// <summary>
        /// 带有checkbox的显示
        /// </summary>
        public static bool ShowAgain(string messageBoxText, string caption,bool DontShowAgain)
        {
            if (DontShowAgain) return DontShowAgain;
            Application.Current.Dispatcher.Invoke(delegate
            {
                Controls.MessageBoxWindow messageBox1 = new Controls.MessageBoxWindow(messageBoxText, caption);
                messageBox1.Topmost = true;
                messageBox1.Owner = Application.Current.MainWindow;
                messageBox1.show_again.Visibility = Visibility.Visible;
                messageBox1.ShowDialog();
                DontShowAgain = messageBox1.DontShowAgain;
            });
            return DontShowAgain;
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
        {
            return Initialize(messageBoxText, caption, button);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            return Initialize(messageBoxText, caption, button, icon);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        {
            return Initialize(messageBoxText, caption, button, icon, defaultResult);
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options)
        {
            return MessageBox.Show(messageBoxText, caption, button, icon, defaultResult);
        }

        private static MessageBoxResult Initialize(Window? owner, string messageBoxText, string caption = "提示", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            Controls.MessageBoxWindow messageBox1 = new Controls.MessageBoxWindow(messageBoxText, caption, button, icon, defaultResult);
            messageBox1.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            messageBox1.Owner = owner;
            messageBox1.ShowDialog();
            return messageBox1.MessageBoxResult;
        }

        public static MessageBoxResult Show(Window? owner, string messageBoxText)
        {
            return Initialize(owner,messageBoxText);
        }

        public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption)
        {
            return Initialize(owner, messageBoxText, caption);
        }

        public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button)
        {
            return Initialize(owner, messageBoxText, caption, button);
        }

        public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            return Initialize(owner, messageBoxText, caption, button, icon);
        }

        public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        {
            return Initialize(owner, messageBoxText, caption, button, icon, defaultResult);
        }

        public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options)
        {
            return MessageBox.Show(owner, messageBoxText, caption, button, icon, defaultResult);
        }
    }
}
