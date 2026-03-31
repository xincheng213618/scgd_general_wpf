using ColorVision.Themes;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Desktop.ThirdPartyApps
{
    public partial class AddCustomAppWindow : Window
    {
        public CustomAppEntry? Result { get; private set; }

        private readonly CustomAppEntry? _editing;

        public AddCustomAppWindow(CustomAppEntry? editing = null)
        {
            _editing = editing;
            InitializeComponent();
            this.ApplyCaption();

            if (_editing != null)
            {
                Title = "编辑自定义应用";
                TxtName.Text = _editing.Name;
                TxtCommand.Text = _editing.Command;
                TxtArguments.Text = _editing.Arguments;
                TxtWorkingDir.Text = _editing.WorkingDirectory;
                TxtGroup.Text = _editing.Group;

                for (int i = 0; i < CmbType.Items.Count; i++)
                {
                    if (CmbType.Items[i] is ComboBoxItem item && item.Tag?.ToString() == _editing.AppType.ToString())
                    {
                        CmbType.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void CmbType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LblCommand == null) return;

            var selected = CmbType.SelectedItem as ComboBoxItem;
            var tag = selected?.Tag?.ToString();

            switch (tag)
            {
                case "Executable":
                    LblCommand.Text = "路径";
                    BtnBrowse.Visibility = Visibility.Visible;
                    LblHint.Text = "提示：填写 exe 文件的完整路径。分组为空时使用默认分组。";
                    break;
                case "CmdScript":
                    LblCommand.Text = "命令";
                    BtnBrowse.Visibility = Visibility.Collapsed;
                    LblHint.Text = "提示：直接填写 CMD 命令（如 ipconfig /all）。会通过 cmd /c 执行。";
                    break;
                case "PowerShellScript":
                    LblCommand.Text = "命令";
                    BtnBrowse.Visibility = Visibility.Collapsed;
                    LblHint.Text = "提示：填写 PowerShell 命令或 .ps1 脚本路径。会以 -NoProfile -ExecutionPolicy Bypass 执行。";
                    break;
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "可执行文件 (*.exe)|*.exe|批处理文件 (*.bat;*.cmd)|*.bat;*.cmd|所有文件 (*.*)|*.*",
                Title = "选择应用程序"
            };
            if (dlg.ShowDialog() == true)
            {
                TxtCommand.Text = dlg.FileName;
                if (string.IsNullOrWhiteSpace(TxtName.Text))
                {
                    TxtName.Text = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                }
            }
        }

        private void BtnBrowseDir_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "选择工作目录（选择该目录下任意文件）",
                CheckFileExists = false,
                FileName = "选择此文件夹"
            };
            if (dlg.ShowDialog() == true)
            {
                TxtWorkingDir.Text = System.IO.Path.GetDirectoryName(dlg.FileName) ?? string.Empty;
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("请填写应用名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtName.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(TxtCommand.Text))
            {
                MessageBox.Show("请填写路径或命令", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCommand.Focus();
                return;
            }

            var typeTag = (CmbType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Executable";
            var appType = Enum.TryParse<CustomAppType>(typeTag, out var parsed) ? parsed : CustomAppType.Executable;

            Result = new CustomAppEntry
            {
                Name = TxtName.Text.Trim(),
                Command = TxtCommand.Text.Trim(),
                Arguments = TxtArguments.Text.Trim(),
                WorkingDirectory = TxtWorkingDir.Text.Trim(),
                Group = TxtGroup.Text.Trim(),
                AppType = appType,
            };

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
