using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Spectrum.Calibration
{
    public partial class SpectrumCalibrationGroupWindow : Window
    {
        private readonly ConfigSpectrum _config;
        private readonly ConfigSpectrum? _draftTarget;
        private readonly (DeviceSpectrum Device, DeviceConfigurationTarget Target)? _live;
        private readonly bool _opensLocally;
        private readonly bool _hasLegacyDirectWheel;
        private readonly Dictionary<int, RateInput> _rateInputs = new();
        private bool _isRefreshing = true;

        public SpectrumCalibrationGroupWindow(DeviceSpectrum device)
            : this(new SpectrumConfigurationDraft(device.Config).Config, device.IsLocalSpectrumBackend, null)
        {
            _live = (device, device.CaptureConfigurationTarget());
        }

        internal SpectrumCalibrationGroupWindow(ConfigSpectrum draft, bool opensLocally)
            : this(new SpectrumConfigurationDraft(draft).Config, opensLocally, draft)
        {
        }

        private SpectrumCalibrationGroupWindow(ConfigSpectrum config, bool opensLocally, ConfigSpectrum? draftTarget)
        {
            _config = config;
            _draftTarget = draftTarget;
            _opensLocally = opensLocally;
            var nd = config.NDConfig;
            _hasLegacyDirectWheel = opensLocally && !nd.IsBingNDDevice && (nd.IsNDPort || nd.LocalNDPortCount.HasValue);

            InitializeComponent();
            ColorVision.Themes.ThemeManagerExtensions.ApplyCaption(this);
            DataContext = config;
            LocalNdEnabled.Visibility = opensLocally ? Visibility.Visible : Visibility.Collapsed;
            if (draftTarget != null) SaveHintText.Text = EngineLocalization.Get("SpectrumCalibrationGroups_DraftSaveHint");
            else if (opensLocally) SaveHintText.Text = EngineLocalization.Get("SpectrumCalibrationGroups_NdLocalSaveHint");

            string currentCode = nd.IsBingNDDevice ? nd.NDBindDeviceCode ?? string.Empty : string.Empty;
            var wheels = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCfwPort>().OrderBy(wheel => wheel.Name)
                .Select(wheel => new CfwServiceOption(wheel.Code, string.IsNullOrWhiteSpace(wheel.Name) ? wheel.Code : $"{wheel.Name} ({wheel.Code})", wheel)).ToList();
            wheels.Insert(0, new CfwServiceOption(string.Empty, Properties.Resources.SpectrumCalibrationGroups_NotSelected));
            if (currentCode.Length > 0 && wheels.All(option => option.Code != currentCode))
                wheels.Add(new CfwServiceOption(currentCode, string.Format(Properties.Resources.SpectrumCalibrationGroups_NotLoadedFormat, currentCode)));
            ComboBoxCfwService.ItemsSource = wheels;
            ComboBoxCfwService.SelectedValue = currentCode;

            config.EnsureCalibrationGroups();
            ComboBoxGroups.ItemsSource = config.CalibrationGroups;
            ComboBoxGroups.SelectedItem = config.ActiveCalibrationGroup;
            ShowSelectedGroup();
            RefreshNdHoleOptions();
            nd.PropertyChanged += NdConfig_PropertyChanged;
            Closed += (_, _) => nd.PropertyChanged -= NdConfig_PropertyChanged;
        }

        private SpectrumCalibrationGroup? SelectedGroup => ComboBoxGroups.SelectedItem as SpectrumCalibrationGroup;

        private void RefreshNdHoleOptions()
        {
            _isRefreshing = true;
            var selected = ComboBoxCfwService.SelectedItem as CfwServiceOption;
            bool hasBinding = !string.IsNullOrEmpty(selected?.Code);
            bool legacyDirect = !hasBinding && _hasLegacyDirectWheel;
            var options = new List<NdHoleOption> { new(-1, Properties.Resources.SpectrumCalibrationGroups_NoAssociation) };
            if (selected?.Wheel is { } wheel)
            {
                options.AddRange(DeviceSpectrum.ResolveNdHoleMappings(wheel).OrderBy(hole => hole.HoleIndex)
                    .Select(hole => new NdHoleOption(hole.HoleIndex, $"{hole.HoleIndex}: {hole.HoleName}")));
            }
            else if (legacyDirect)
            {
                options.AddRange(DeviceSpectrum.ResolveNdHoleMappings(null, LocalSpectrumNd.GetDirectPortCount(_config.NDConfig))
                    .Select(hole => new NdHoleOption(hole.HoleIndex, $"{hole.HoleIndex}: {hole.HoleName}")));
            }

            int savedHole = SelectedGroup?.NDHoleIndex ?? -1;
            ComboBoxNDHole.ItemsSource = options;
            ComboBoxNDHole.SetCurrentValue(ComboBox.SelectedValueProperty, savedHole);
            LocalNdEnabled.IsEnabled = hasBinding || legacyDirect;
            _isRefreshing = false;
            ShowAttenuation();
        }

        private void ComboBoxCfwService_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshing) return;

            var nd = _config.NDConfig;
            string code = ComboBoxCfwService.SelectedValue as string ?? string.Empty;
            nd.IsBingNDDevice = code.Length > 0;
            nd.NDBindDeviceCode = code;
            if (_opensLocally && code.Length == 0 && !_hasLegacyDirectWheel) nd.IsNDPort = false;
            RefreshNdHoleOptions();
        }

        private void ComboBoxGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isRefreshing) ShowSelectedGroup();
        }

        private void ShowSelectedGroup()
        {
            DetailGrid.DataContext = SelectedGroup;
            NdHoleFields.DataContext = SelectedGroup;
            ShowAttenuation();
        }

        private void ComboBoxNDHole_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isRefreshing) ShowAttenuation();
        }

        private void NdConfig_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NDConfig.IsNDPort)) ShowAttenuation();
        }

        private void ShowAttenuation()
        {
            var nd = _config.NDConfig;
            RateInput? input = null;
            if (_opensLocally && nd.IsNDPort && ComboBoxNDHole.SelectedItem is NdHoleOption { HoleIndex: >= 0 } hole)
            {
                if (!_rateInputs.TryGetValue(hole.HoleIndex, out input))
                {
                    int? rate = nd.LocalNDRates != null
                        ? nd.LocalNDRates.TryGetValue(hole.HoleIndex, out int explicitRate) ? explicitRate : null
                        : hole.HoleIndex < nd.NDRate.Count ? nd.NDRate[hole.HoleIndex] : null;
                    _rateInputs.Add(hole.HoleIndex, input = new RateInput(rate));
                }
            }

            NdAttenuationField.DataContext = input;
            NdAttenuationField.Visibility = input != null ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnAddGroup_Click(object sender, RoutedEventArgs e)
        {
            var group = new SpectrumCalibrationGroup
            {
                GroupName = CreateGroupName(),
                WavelengthFile = _config.WavelengthFile,
                MaguideFile = _config.MaguideFile,
            };
            _config.CalibrationGroups.Add(group);
            ComboBoxGroups.SelectedItem = group;
        }

        private void BtnRemoveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (_config.CalibrationGroups.Count <= 1)
            {
                MessageBox.Show(this, Properties.Resources.SpectrumCalibrationGroups_KeepAtLeastOneGroup, "ColorVision");
                return;
            }

            if (SelectedGroup == null) return;
            _config.CalibrationGroups.Remove(SelectedGroup);
            ComboBoxGroups.SelectedItem = _config.CalibrationGroups.FirstOrDefault();
        }

        private void BtnSelectWavelength_Click(object sender, RoutedEventArgs e)
        {
            SelectFile(SelectedGroup?.WavelengthFile, file => SelectedGroup!.WavelengthFile = file);
        }

        private void BtnSelectMaguide_Click(object sender, RoutedEventArgs e)
        {
            SelectFile(SelectedGroup?.MaguideFile, file => SelectedGroup!.MaguideFile = file);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TrySaveChanges(out string error))
                {
                    Close();
                    return;
                }
                MessageBox.Show(this, error, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, EngineLocalization.Format($"保存标定配置失败：{ex.Message}"), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        internal bool TrySaveChanges(out string error)
        {
            error = string.Empty;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (SelectedGroup is not { } group || _config.CalibrationGroups.Any(item =>
                string.IsNullOrWhiteSpace(item.GroupName) || !names.Add(item.GroupName.Trim())))
            {
                error = EngineLocalization.Get("SpectrumCalibrationGroups_InvalidNames");
                return false;
            }
            if (_opensLocally && _config.NDConfig.IsNDPort && _config.CalibrationGroups
                .Where(item => item.NDHoleIndex >= 0).GroupBy(item => item.NDHoleIndex).Any(items => items.Count() > 1))
            {
                error = EngineLocalization.Get("SpectrumCalibrationGroups_NdInvalidHoles");
                return false;
            }
            if (!TryApplyEditedRates(out error)) return false;

            _config.ActiveCalibrationGroupName = group.GroupName;
            _config.WavelengthFile = group.WavelengthFile;
            _config.MaguideFile = group.MaguideFile;
            if (_live is { } live)
            {
                if (!live.Device.CanConfigure)
                {
                    error = EngineLocalization.Get("当前权限不足，无法保存设备配置。");
                    return false;
                }
                if (live.Device.TrySaveCalibrationGroupConfiguration(_config, live.Target)) return true;
                error = EngineLocalization.Get("设备标定配置已变更，请重新打开后编辑。");
                return false;
            }

            _draftTarget!.ApplyCalibrationAndNdSettings(_config);
            return true;
        }

        private bool TryApplyEditedRates(out string error)
        {
            error = string.Empty;
            var edits = _rateInputs.Where(pair => pair.Value.IsEdited).ToArray();
            if (edits.Length == 0) return true;

            var nd = _config.NDConfig;
            var rates = nd.LocalNDRates != null ? new Dictionary<int, int>(nd.LocalNDRates)
                : nd.NDRate.Select((rate, hole) => KeyValuePair.Create(hole, rate)).Where(pair => pair.Key != nd.DarkNDPort).ToDictionary();
            foreach (var (hole, input) in edits)
            {
                if (string.IsNullOrWhiteSpace(input.Text))
                {
                    rates.Remove(hole);
                    continue;
                }
                if (!int.TryParse(input.Text, out int rate) || rate < 0 || hole == nd.DarkNDPort)
                {
                    error = EngineLocalization.Get("SpectrumCalibrationGroups_NdInvalidRates");
                    return false;
                }

                var group = _config.CalibrationGroups.FirstOrDefault(item => item.NDHoleIndex == hole);
                if (group == null || string.IsNullOrWhiteSpace(group.WavelengthFile) || string.IsNullOrWhiteSpace(group.MaguideFile))
                {
                    error = string.Format(CultureInfo.CurrentCulture, EngineLocalization.Get("SpectrumCalibrationGroups_NdCalibrationRequired"), hole);
                    return false;
                }
                rates[hole] = rate;
            }
            if (rates.Values.Distinct().Count() != rates.Count)
            {
                error = EngineLocalization.Get("SpectrumCalibrationGroups_NdInvalidRates");
                return false;
            }

            nd.LocalNDRates = rates;
            return true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void SelectFile(string? currentPath, Action<string> apply)
        {
            if (SelectedGroup == null) return;
            if (SpectrumCalibrationFilePicker.Browse(this, currentPath, _live?.Device.IsLocalSpectrumBackend ?? _opensLocally) is string file)
                apply(file);
        }

        private string CreateGroupName()
        {
            for (int i = 1; i < 999; i++)
            {
                string name = $"Group{i}";
                if (_config.CalibrationGroups.All(item => !string.Equals(item.GroupName, name, StringComparison.OrdinalIgnoreCase)))
                    return name;
            }
            return $"Group{_config.CalibrationGroups.Count + 1}";
        }

        private sealed class RateInput(int? rate)
        {
            private readonly string _initial = rate?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            public string Text { get; set; } = rate?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            internal bool IsEdited => !string.Equals(_initial, Text.Trim(), StringComparison.Ordinal);
        }

        private sealed record NdHoleOption(int HoleIndex, string DisplayName);
        private sealed record CfwServiceOption(string Code, string DisplayName, DeviceCfwPort? Wheel = null);
    }
}
