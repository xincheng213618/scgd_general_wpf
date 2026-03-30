using log4net;
using Newtonsoft.Json;
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
        private bool _suppressSelectionChanged;
        private bool _hasUnsavedChanges;

        // Backup of initial state for restore on discard
        private readonly string _initialConfigJson;
        private readonly string _initialWavelengthFile;
        private readonly string _initialMaguideFile;

        public CalibrationGroupWindow(SpectrometerManager manager)
        {
            Manager = manager;

            // Save initial state before any edits
            _initialConfigJson = JsonConvert.SerializeObject(Manager.CalibrationGroupConfig);
            _initialWavelengthFile = Manager.WavelengthFile;
            _initialMaguideFile = Manager.MaguideFile;

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
            ComboBoxGroups.ItemsSource = Manager.CalibrationGroupConfig.Groups;
            ComboBoxGroups.DisplayMemberPath = "GroupName";

            var active = Manager.CalibrationGroupConfig.ActiveGroup;
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
                // Set directly on config without triggering save/apply side-effects
                Manager.CalibrationGroupConfig.ActiveGroupName = group.GroupName;
            }
            UpdateGroupDetail();
        }

        private void BtnAddGroup_Click(object sender, RoutedEventArgs e)
        {
            // Add group directly to config without auto-saving
            int idx = Manager.CalibrationGroupConfig.Groups.Count;
            string newName;
            do
            {
                newName = $"Group{idx}";
                idx++;
            } while (Manager.CalibrationGroupConfig.Groups.Any(g => g.GroupName == newName));

            Manager.CalibrationGroupConfig.Groups.Add(new CalibrationGroup
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
            if (Manager.CalibrationGroupConfig.Groups.Count <= 1)
            {
                MessageBox.Show("至少保留一个分组", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Remove group directly from config without auto-saving
            var group = ComboBoxGroups.SelectedItem as CalibrationGroup;
            if (group == null) return;
            Manager.CalibrationGroupConfig.Groups.Remove(group);
            Manager.CalibrationGroupConfig.ActiveGroupName = Manager.CalibrationGroupConfig.Groups.FirstOrDefault()?.GroupName ?? "Default";
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
                if (Manager.CalibrationGroupConfig.Groups.Any(g => g != group && g.GroupName == newName))
                    return;

                group.GroupName = newName;
                // Set directly on config without triggering save/apply side-effects
                Manager.CalibrationGroupConfig.ActiveGroupName = newName;
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

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ApplyAndSave();
            MarkSaved();
            MessageBox.Show("标定配置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Applies the current edits to Manager and saves to disk.
        /// </summary>
        private void ApplyAndSave()
        {
            // Apply active group files to Manager
            var group = Manager.CalibrationGroupConfig.ActiveGroup;
            if (group != null)
            {
                Manager.WavelengthFile = group.WavelengthFile;
                Manager.MaguideFile = group.MaguideFile;
            }
            Manager.SaveCalibrationConfig();
        }

        /// <summary>
        /// Discards all in-memory changes and restores the initial state.
        /// </summary>
        private void DiscardChanges()
        {
            var restored = JsonConvert.DeserializeObject<CalibrationGroupConfig>(_initialConfigJson);
            if (restored != null)
            {
                Manager.CalibrationGroupConfig.Groups.Clear();
                foreach (var g in restored.Groups)
                    Manager.CalibrationGroupConfig.Groups.Add(g);
                Manager.CalibrationGroupConfig.ActiveGroupName = restored.ActiveGroupName;
            }
            else
            {
                log.Warn("Failed to deserialize initial config snapshot during discard");
            }
            Manager.WavelengthFile = _initialWavelengthFile;
            Manager.MaguideFile = _initialMaguideFile;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show("有未保存的更改，是否保存？", "提示", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    ApplyAndSave();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                else
                {
                    DiscardChanges();
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
