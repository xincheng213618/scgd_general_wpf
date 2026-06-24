using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Spectrum.Calibration
{
    public partial class SpectrumCalibrationGroupWindow : Window
    {
        private readonly DeviceSpectrum _device;
        private readonly string _initialBindDeviceCode;
        private bool _isRefreshing;

        public SpectrumCalibrationGroupWindow(DeviceSpectrum device)
        {
            _device = device;
            _initialBindDeviceCode = _device.Config.NDConfig.NDBindDeviceCode ?? string.Empty;
            InitializeComponent();
            DataContext = _device;
            RefreshCfwServiceOptions();
            RefreshNDHoleOptions();
            RefreshGroups();
        }

        private SpectrumCalibrationGroup? SelectedGroup => ComboBoxGroups.SelectedItem as SpectrumCalibrationGroup;

        private void RefreshGroups()
        {
            _isRefreshing = true;
            _device.Config.EnsureCalibrationGroups();
            ComboBoxGroups.ItemsSource = null;
            ComboBoxGroups.ItemsSource = _device.Config.CalibrationGroups;
            ComboBoxGroups.SelectedItem = _device.Config.ActiveCalibrationGroup;
            DetailGrid.DataContext = ComboBoxGroups.SelectedItem;
            _isRefreshing = false;
        }

        private void RefreshCfwServiceOptions()
        {
            _isRefreshing = true;
            string currentCode = _device.Config.NDConfig.NDBindDeviceCode ?? string.Empty;
            var options = ServiceManager.GetInstance().DeviceServices
                .OfType<DeviceCfwPort>()
                .OrderBy(a => a.Name)
                .Select(a => new CfwServiceOption(a.Code, string.IsNullOrWhiteSpace(a.Name) ? a.Code : $"{a.Name} ({a.Code})"))
                .ToList();

            options.Insert(0, new CfwServiceOption(string.Empty, "未选择"));
            if (!string.IsNullOrWhiteSpace(currentCode) && options.All(a => a.Code != currentCode))
                options.Add(new CfwServiceOption(currentCode, $"{currentCode} (未加载)"));

            ComboBoxCfwService.ItemsSource = options;
            ComboBoxCfwService.SelectedValue = currentCode;
            _isRefreshing = false;
        }

        private void RefreshNDHoleOptions()
        {
            var options = new List<NDHoleOption> { new NDHoleOption(-1, "无关联") };
            options.AddRange(_device.GetNDHoleMappings().OrderBy(a => a.HoleIndex).Select(a => new NDHoleOption(a.HoleIndex, $"{a.HoleIndex}: {a.HoleName}")));
            ComboBoxNDHole.ItemsSource = options;
        }

        private void ComboBoxGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshing)
                return;

            DetailGrid.DataContext = SelectedGroup;
            if (SelectedGroup != null)
                _device.Config.ActiveCalibrationGroupName = SelectedGroup.GroupName;
        }

        private void ComboBoxCfwService_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshing)
                return;

            string code = ComboBoxCfwService.SelectedValue as string ?? string.Empty;
            if (string.Equals(_device.Config.NDConfig.NDBindDeviceCode, code, StringComparison.Ordinal))
                return;

            _device.Config.NDConfig.IsBingNDDevice = true;
            _device.Config.NDConfig.NDBindDeviceCode = code;
            RefreshNDHoleOptions();
        }

        private void BtnAddGroup_Click(object sender, RoutedEventArgs e)
        {
            var group = new SpectrumCalibrationGroup
            {
                GroupName = CreateGroupName(),
                WavelengthFile = _device.Config.WavelengthFile,
                MaguideFile = _device.Config.MaguideFile,
            };

            _device.Config.CalibrationGroups.Add(group);
            ComboBoxGroups.SelectedItem = group;
        }

        private void BtnRemoveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (_device.Config.CalibrationGroups.Count <= 1)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "至少保留一个分组", "ColorVision");
                return;
            }

            if (SelectedGroup == null)
                return;

            _device.Config.CalibrationGroups.Remove(SelectedGroup);
            ComboBoxGroups.SelectedItem = _device.Config.CalibrationGroups.FirstOrDefault();
        }

        private void BtnSelectWavelength_Click(object sender, RoutedEventArgs e)
        {
            SelectFile(file => SelectedGroup!.WavelengthFile = file);
        }

        private void BtnSelectMaguide_Click(object sender, RoutedEventArgs e)
        {
            SelectFile(file => SelectedGroup!.MaguideFile = file);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedGroup != null)
                _device.Config.ActiveCalibrationGroupName = SelectedGroup.GroupName;

            bool ndServiceChanged = !string.Equals(_initialBindDeviceCode, _device.Config.NDConfig.NDBindDeviceCode ?? string.Empty, StringComparison.Ordinal);
            if (!_device.ApplyActiveCalibrationGroup(true))
            {
                if (ndServiceChanged)
                    _device.Save();
                else
                    _device.SaveConfig();
            }

            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SelectFile(Action<string> apply)
        {
            if (SelectedGroup == null)
                return;

            using var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "DAT files (*.dat)|*.dat|All Files|*.*",
                RestoreDirectory = true,
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                apply(dialog.FileName);
        }

        private string CreateGroupName()
        {
            for (int i = 1; i < 999; i++)
            {
                string name = $"Group{i}";
                if (_device.Config.CalibrationGroups.All(a => !string.Equals(a.GroupName, name, StringComparison.OrdinalIgnoreCase)))
                    return name;
            }
            return $"Group{_device.Config.CalibrationGroups.Count + 1}";
        }

        private sealed class NDHoleOption
        {
            public NDHoleOption(int holeIndex, string displayName)
            {
                HoleIndex = holeIndex;
                DisplayName = displayName;
            }

            public int HoleIndex { get; }
            public string DisplayName { get; }
        }

        private sealed class CfwServiceOption
        {
            public CfwServiceOption(string code, string displayName)
            {
                Code = code;
                DisplayName = displayName;
            }

            public string Code { get; }
            public string DisplayName { get; }
        }
    }
}
