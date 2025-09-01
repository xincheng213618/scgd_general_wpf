using ColorVision.Common.Utilities;
using ColorVision.Engine.Messages;
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Services
{

    internal static partial class ServicesHelper
    {

        public static async void SelectAndFocusFirstNode(TreeView treeView)
        {
            await Task.Delay(100);
            if (treeView.Items.Count > 0)
            {
                if (treeView.SelectedItem == null && treeView.ItemContainerGenerator.ContainerFromIndex(0) is TreeViewItem firstNode)
                {
                    firstNode.IsSelected = true;
                    Application.Current.Dispatcher.Invoke(() => firstNode.Focus());
                }
            }
        }

        public static MsgRecord? SendCommandEx(object sender, Func<MsgRecord> action)
        {
            if (sender is Button button)
            {
                if (button.Content.ToString() == (Properties.Resources.ResourceManager.GetString(MsgRecordState.Sended.ToDescription(), CultureInfo.CurrentUICulture) ?? ""))
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "已经发送,请耐心等待","ColorVison");
                    return null;
                }
                MsgRecord msgRecord = action.Invoke();
                SendCommand(button, msgRecord);
                return msgRecord;
            }
            return null;
        }

        public static void SendCommand(object sender, MsgRecord msgRecord)
        {
            if (sender is Button button)
            {
                var temp = button.Content;
                button.Content = Properties.Resources.ResourceManager.GetString(msgRecord.MsgRecordState.ToDescription(), CultureInfo.CurrentUICulture) ?? "";

                MsgRecordStateChangedHandler msgRecordStateChangedHandler = null;
                msgRecordStateChangedHandler = (e) =>
                {
                    button.Content = temp;
                    msgRecord.MsgRecordStateChanged -= msgRecordStateChangedHandler;
                };
                msgRecord.MsgRecordStateChanged += msgRecordStateChangedHandler;
            }
        }

        public static void SendCommand(Button button, MsgRecord msgRecord, bool Reserve = true)
        {
            var temp = button.Content;
            button.Content = Properties.Resources.ResourceManager.GetString(msgRecord.MsgRecordState.ToDescription(), CultureInfo.CurrentUICulture) ?? "";

            MsgRecordStateChangedHandler msgRecordStateChangedHandler = null;
            msgRecordStateChangedHandler = (e) =>
            {
                button.Content = temp;
                msgRecord.MsgRecordStateChanged -= msgRecordStateChangedHandler;
            };
            msgRecord.MsgRecordStateChanged += msgRecordStateChangedHandler;
        }

        public static bool IsInvalidPath(string Path, string Hint = "名称")
        {
            if (string.IsNullOrEmpty(Path))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(),$"{Hint}不能为空", "ColorVision");
                return false;
            }
            if (string.IsNullOrWhiteSpace(Path))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{Hint}不能为空白", "ColorVision");
                return false;
            }
            if (Path.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), $"{Hint}不能包含特殊字符", "ColorVision");
                return false;
            }
            return true;
        }

    }
}
