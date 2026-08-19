using log4net;
using Spectrum.Configs;
using System.Windows;
using System.Windows.Controls;

namespace Spectrum.Calibration
{
    /// <summary>
    /// CalibrationGroupWindow.xaml — manages calibration groups for a specific spectrometer SN.
    /// Changes are staged and only saved when user clicks "保存".
    /// Closing without saving discards all in-memory changes.
    /// </summary>
    public partial class CalibrationGroupWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CalibrationGroupWindow));

        private SpectrometerManager Manager { get; }
        private CalibrationGroupConfig WorkingConfig { get; }
        private bool _suppressSelectionChanged;
        private bool _hasUnsavedChanges;
        private bool _saveInProgress;

        public CalibrationGroupWindow(SpectrometerManager manager)
        {
            Manager = manager;
            WorkingConfig = Manager.CalibrationGroupConfig.Clone();

            InitializeComponent();
            RefreshGroupList();
            UpdateConfigPathDisplay();
            _hasUnsavedChanges = false;
        }

        private void MarkUnsaved()
        {
            _hasUnsavedChanges = true;
            Title = "标定文件分组管理 *";
        }

        private void MarkSaved()
        {
            _hasUnsavedChanges = false;
            Title = "标定文件分组管理";
        }

        private void RefreshGroupList()
        {
            _suppressSelectionChanged = true;
            ComboBoxGroups.ItemsSource = null;
            ComboBoxGroups.ItemsSource = WorkingConfig.Groups;
            ComboBoxGroups.DisplayMemberPath = "GroupName";

            var active = WorkingConfig.ActiveGroup;
            if (active != null)
                ComboBoxGroups.SelectedItem = active;
            else if (ComboBoxGroups.Items.Count > 0)
                ComboBoxGroups.SelectedIndex = 0;

            _suppressSelectionChanged = false;
            UpdateGroupDetail();
        }

        private void UpdateGroupDetail()
        {
            var group = ComboBoxGroups.SelectedItem as CalibrationGroup;
            if (group == null)
            {
                TextBoxGroupName.Text = string.Empty;
                TextBoxWavelengthFile.Text = string.Empty;
                TextBoxMaguideFile.Text = string.Empty;
                TextWavelengthValidation.Text = string.Empty;
                TextMaguideValidation.Text = string.Empty;
                TextWavelengthStatus.Text = "---";
                TextMaguideStatus.Text = "---";
                ComboBoxFilterWheelPosition.SelectedIndex = 0; // "无关联"
                return;
            }

            TextBoxGroupName.Text = group.GroupName;
            TextBoxWavelengthFile.Text = group.WavelengthFile;
            TextBoxMaguideFile.Text = group.MaguideFile;

            // Set filter wheel position combo (-1 maps to index 0 "无关联", 0-4 maps to index 1-5)
            int fwIndex = group.FilterWheelPosition + 1;
            if (fwIndex >= 0 && fwIndex < ComboBoxFilterWheelPosition.Items.Count)
                ComboBoxFilterWheelPosition.SelectedIndex = fwIndex;
            else
                ComboBoxFilterWheelPosition.SelectedIndex = 0;

            // Auto-validate on selection
            ValidateWavelength(group.WavelengthFile);
            ValidateMaguide(group.MaguideFile);
        }

        private void UpdateConfigPathDisplay()
        {
            if (!string.IsNullOrEmpty(Manager.SerialNumber))
                TextConfigPath.Text = CalibrationGroupConfig.GetConfigDirectory(Manager.SerialNumber);
            else
                TextConfigPath.Text = "设备未连接，SN未知";
        }

        private void ComboBoxGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged) return;
            var group = ComboBoxGroups.SelectedItem as CalibrationGroup;
            if (group != null)
            {
                WorkingConfig.ActiveGroupName = group.GroupName;
                MarkUnsaved();
            }
            UpdateGroupDetail();
        }

        private void BtnAddGroup_Click(object sender, RoutedEventArgs e)
        {
            // Add group directly to config without auto-saving
            int idx = WorkingConfig.Groups.Count;
            string newName;
            do
            {
                newName = $"Group{idx}";
                idx++;
            } while (WorkingConfig.Groups.Any(g => g.GroupName == newName));

            WorkingConfig.Groups.Add(new CalibrationGroup
            {
                GroupName = newName,
                WavelengthFile = Manager.WavelengthFile,
                MaguideFile = Manager.MaguideFile,
            });

            RefreshGroupList();
            // Select the newly added group (last one)
            if (ComboBoxGroups.Items.Count > 0)
                ComboBoxGroups.SelectedIndex = ComboBoxGroups.Items.Count - 1;
            MarkUnsaved();
        }

        private void BtnRemoveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (WorkingConfig.Groups.Count <= 1)
            {
                MessageBox.Show("至少保留一个分组", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Remove group directly from config without auto-saving
            var group = ComboBoxGroups.SelectedItem as CalibrationGroup;
            if (group == null) return;
            WorkingConfig.Groups.Remove(group);
            WorkingConfig.ActiveGroupName = WorkingConfig.Groups.FirstOrDefault()?.GroupName ?? "Default";
            RefreshGroupList();
            MarkUnsaved();
        }

        private void TextBoxGroupName_TextChanged(object sender, TextChangedEventArgs e)
        {
            var group = ComboBoxGroups.SelectedItem as CalibrationGroup;
            if (group == null || string.IsNullOrWhiteSpace(TextBoxGroupName.Text)) return;

            string newName = TextBoxGroupName.Text.Trim();
            if (newName != group.GroupName)
            {
                // Check for duplicate group names
                if (WorkingConfig.Groups.Any(g => g != group && g.GroupName == newName))
                    return;

                group.GroupName = newName;
                WorkingConfig.ActiveGroupName = newName;
                MarkUnsaved();

                // Refresh ComboBox display
                _suppressSelectionChanged = true;
                ComboBoxGroups.Items.Refresh();
                _suppressSelectionChanged = false;
            }
        }

        private void BtnSelectWavelength_Click(object sender, RoutedEventArgs e)
        {
            var group = ComboBoxGroups.SelectedItem as CalibrationGroup;
            if (group == null) return;

            using var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Filter = "DAT files (*.dat)|*.dat|All Files|*.*";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                group.WavelengthFile = dialog.FileName;
                TextBoxWavelengthFile.Text = dialog.FileName;
                MarkUnsaved();
                ValidateWavelength(dialog.FileName);
            }
        }

        private void BtnSelectMaguide_Click(object sender, RoutedEventArgs e)
        {
            var group = ComboBoxGroups.SelectedItem as CalibrationGroup;
            if (group == null) return;

            using var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Filter = "DAT files (*.dat)|*.dat|All Files|*.*";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                group.MaguideFile = dialog.FileName;
                TextBoxMaguideFile.Text = dialog.FileName;
                MarkUnsaved();
                ValidateMaguide(dialog.FileName);
            }
        }

        private void BtnValidateWavelength_Click(object sender, RoutedEventArgs e)
        {
            ValidateWavelength(TextBoxWavelengthFile.Text);
        }

        private void BtnValidateMaguide_Click(object sender, RoutedEventArgs e)
        {
            ValidateMaguide(TextBoxMaguideFile.Text);
        }

        private void ValidateWavelength(string filePath)
        {
            var result = CalibrationFileValidator.ValidateWavelengthFile(filePath);
            TextWavelengthValidation.Text = result.Message;
            TextWavelengthValidation.Foreground = result.IsValid
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.OrangeRed;
            TextWavelengthStatus.Text = result.IsValid
                ? $"✓ {result.DataCount} 个数据点"
                : $"✗ {result.Message}";
            TextWavelengthStatus.Foreground = result.IsValid
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.OrangeRed;
        }

        private void ValidateMaguide(string filePath)
        {
            var result = CalibrationFileValidator.ValidateMaguideFile(filePath);
            TextMaguideValidation.Text = result.Message;
            TextMaguideValidation.Foreground = result.IsValid
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.OrangeRed;
            TextMaguideStatus.Text = result.IsValid
                ? $"✓ {result.DataCount} 点, 积分={result.MagExpTime}ms"
                : $"✗ {result.Message}";
            TextMaguideStatus.Foreground = result.IsValid
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.OrangeRed;
        }

        private void ComboBoxFilterWheelPosition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged) return;
            var group = ComboBoxGroups.SelectedItem as CalibrationGroup;
            if (group == null) return;

            if (ComboBoxFilterWheelPosition.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int pos))
            {
                group.FilterWheelPosition = pos;
                MarkUnsaved();
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_saveInProgress)
                return;

            _saveInProgress = true;
            IsEnabled = false;
            try
            {
                SpectrumCalibrationApplyResult result = await ApplyAndSaveAsync();
                if (!result.IsSuccess)
                {
                    MessageBox.Show(result.ErrorMessage, "标定配置未完成", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                MarkSaved();
                string message = Manager.IsConnected ? "标定配置已保存并加载" : "标定配置已保存，将在连接光谱仪时加载";
                MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                IsEnabled = true;
                _saveInProgress = false;
            }
        }

        /// <summary>
        /// Applies the current edits to Manager and saves to disk.
        /// </summary>
        private async Task<SpectrumCalibrationApplyResult> ApplyAndSaveAsync()
        {
            // Apply active group files to Manager
            CalibrationGroupConfig candidate = WorkingConfig.Clone();
            CalibrationGroup? group = candidate.ActiveGroup;
            if (group == null)
                return new SpectrumCalibrationApplyResult(false, "请选择一个有效的标定分组");

            SpectrumCalibrationApplyResult result = Manager.IsConnected
                ? await Manager.ApplyCalibrationAsync(
                    group.GroupName,
                    group.WavelengthFile,
                    group.MaguideFile).ConfigureAwait(false)
                : SpectrumCalibrationApplyResult.Success;
            if (result.IsSuccess && !Manager.CommitCalibrationConfiguration(candidate, result.RequestVersion))
            {
                string saveError = Manager.LastOperationError;
                if (Manager.IsConnected && Manager.IsCalibrationRequestCurrent(result.RequestVersion))
                {
                    SpectrumCalibrationApplyResult restore = await Manager
                        .RestoreConfiguredCalibrationAsync(result.RequestVersion)
                        .ConfigureAwait(false);
                    if (!restore.IsSuccess)
                        saveError = $"{saveError}；恢复原标定失败：{restore.ErrorMessage}";
                }
                return new SpectrumCalibrationApplyResult(false, saveError);
            }
            return result;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_saveInProgress)
            {
                e.Cancel = true;
                return;
            }

            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show("有未保存的更改，是否保存？", "提示", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        SpectrumCalibrationApplyResult applyResult = ApplyAndSaveAsync().GetAwaiter().GetResult();
                        if (!applyResult.IsSuccess)
                        {
                            MessageBox.Show(applyResult.ErrorMessage, "标定文件加载失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                            e.Cancel = true;
                            return;
                        }
                        MarkSaved();
                    }
                    catch (Exception ex)
                    {
                        log.Error("Failed to save and apply calibration files", ex);
                        MessageBox.Show(ex.GetBaseException().Message, "标定文件加载失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        e.Cancel = true;
                        return;
                    }
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnClosing(e);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
