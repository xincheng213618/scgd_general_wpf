using ColorVision.Themes;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using ColorVision.UI.Desktop.Properties;

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
                Title = Properties.Resources.CustomApp_EditTitle;
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
                    LblCommand.Text = Properties.Resources.CustomApp_Path;
                    BtnBrowse.Visibility = Visibility.Visible;
                    LblHint.Text = Properties.Resources.CustomApp_HintPath;
                    break;
                case "CmdScript":
                    LblCommand.Text = Properties.Resources.CustomApp_Command;
                    BtnBrowse.Visibility = Visibility.Collapsed;
                    LblHint.Text = Properties.Resources.CustomApp_HintCmd;
                    break;
                case "PowerShellScript":
                    LblCommand.Text = Properties.Resources.CustomApp_Command;
                    BtnBrowse.Visibility = Visibility.Collapsed;
                    LblHint.Text = Properties.Resources.CustomApp_HintPs;
                    break;
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = Properties.Resources.CustomApp_ExeFilter,
                Title = Properties.Resources.CustomApp_SelectApp
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
                Title = Properties.Resources.CustomApp_SelectWorkDir,
                CheckFileExists = false,
                FileName = Properties.Resources.CustomApp_SelectFolder
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
                MessageBox.Show(Properties.Resources.CustomApp_NameRequired, Properties.Resources.ConfigManager_Prompt, MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtName.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(TxtCommand.Text))
            {
                MessageBox.Show(Properties.Resources.CustomApp_PathRequired, Properties.Resources.ConfigManager_Prompt, MessageBoxButton.OK, MessageBoxImage.Warning);
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
