using ColorVision.Common.Utilities;
using ColorVision.Engine.Messages;
using ColorVision.UI;
using ColorVision.Themes.Controls;
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Services
{

    internal static partial class ServicesHelper
    {
        public static bool IsTemplateSelected(ComboBox comboBox, string errorMessage)
        {
            if (comboBox.SelectedIndex == -1)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), errorMessage, "ColorVision");
                return false;
            }
            return true;
        }

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
                    MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.AlreadySentPleaseWait,"ColorVision");
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

                msgRecord.MsgRecordStateChanged += (s,e) => button.Content = temp; 
            }
        }

        public static void SendCommand(Button button, MsgRecord msgRecord, bool Reserve = true)
        {
            var temp = button.Content;
            button.Content = Properties.Resources.ResourceManager.GetString(msgRecord.MsgRecordState.ToDescription(), CultureInfo.CurrentUICulture) ?? "";
            msgRecord.MsgRecordStateChanged += (s,e) => button.Content = temp;
        }

        public static TimedButtonOperationScope? SendTimedCommand(
            FrameworkElement owner,
            Button button,
            MsgRecord msgRecord,
            string? runningText = null,
            Action<MsgRecord, MsgRecordState>? onTerminalStateChanged = null)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(button);
            ArgumentNullException.ThrowIfNull(msgRecord);

            TimedButtonOperationScope? operationScope = owner.GetTimedButtonOperations().Begin(button, runningText: runningText);

            msgRecord.MsgRecordStateChanged += (_, state) =>
            {
                if (state != MsgRecordState.Success && state != MsgRecordState.Fail && state != MsgRecordState.Timeout)
                {
                    return;
                }

                operationScope?.Complete(state == MsgRecordState.Success);
                onTerminalStateChanged?.Invoke(msgRecord, state);
            };

            SendCommand(button, msgRecord);
            return operationScope;
        }

        public static MsgRecord? SendTimedCommand(
            FrameworkElement owner,
            object sender,
            Func<MsgRecord> action,
            string? runningText = null,
            Action<MsgRecord, MsgRecordState>? onTerminalStateChanged = null)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(action);

            if (sender is not Button button)
            {
                return null;
            }

            MsgRecord msgRecord = action.Invoke();
            SendTimedCommand(owner, button, msgRecord, runningText, onTerminalStateChanged);
            return msgRecord;
        }

        public static bool IsInvalidPath(string Path, string? Hint = null)
        {
            Hint ??= Properties.Resources.Name;
            if (string.IsNullOrEmpty(Path))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), string.Format(Properties.Resources.FieldCannotBeEmpty, Hint), "ColorVision");
                return false;
            }
            if (string.IsNullOrWhiteSpace(Path))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), string.Format(Properties.Resources.FieldCannotBeBlank, Hint), "ColorVision");
                return false;
            }
            if (Path.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), string.Format(Properties.Resources.FieldCannotContainSpecialChars, Hint), "ColorVision");
                return false;
            }
            return true;
        }

    }
}
