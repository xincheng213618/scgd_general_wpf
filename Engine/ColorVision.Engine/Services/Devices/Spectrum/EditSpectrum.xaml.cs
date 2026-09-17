using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.PhySpectrums;
using ColorVision.Themes;
using ColorVision.UI;
using cvColorVision;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.Spectrum
{
    public partial class EditSpectrum : Window
    {
        private readonly ConfigSpectrum target;
        private readonly CancellationTokenSource cancellation = new();
        private readonly Dictionary<string, string> discoveries = new(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<PhySpectrum> stored = Array.Empty<PhySpectrum>();
        private IReadOnlyList<PhySpectrum> choices = Array.Empty<PhySpectrum>();
        private bool isBusy;
        private bool closed;

        public SpectrumConfigurationDraft Draft { get; }
        public ConfigSpectrum EditConfig => Draft.Config;

        public EditSpectrum(ConfigSpectrum config)
        {
            target = config;
            Draft = new SpectrumConfigurationDraft(config);
            InitializeComponent();
            this.ApplyCaption();
            DataContext = this;
            InitializeFields();
            EditConfig.PropertyChanged += Config_PropertyChanged;
            RefreshChoices();
            Loaded += async (_, _) => await RunAsync(LoadChoicesAsync);
            Closed += (_, _) =>
            {
                closed = true;
                EditConfig.PropertyChanged -= Config_PropertyChanged;
                cancellation.Cancel();
                if (!isBusy) cancellation.Dispose();
            };
        }

        private void InitializeFields()
        {
            AddFields(ConnectionFields, EditConfig, nameof(ConfigSpectrum.SpectrometerType), nameof(ConfigSpectrum.IsAutoOpen));
            AddFields(SerialFields, Draft, nameof(SpectrumConfigurationDraft.SerialPortName));
            AddFields(SerialFields, EditConfig, nameof(ConfigSpectrum.BaudRate));
            AddFields(CalibrationFields, EditConfig, nameof(ConfigSpectrum.WavelengthFile), nameof(ConfigSpectrum.MaguideFile),
                nameof(ConfigSpectrum.ActiveCalibrationGroupName), nameof(ConfigSpectrum.CalibrationGroups));
            AddGroup(CalibrationFields, Properties.Resources.SpectrumDarkSettings, EditConfig.SelfAdaptionInitDark);
            AddGroup(CalibrationFields, Properties.Resources.EmissionSP100Set, EditConfig.SetEmissionSP100Config);
            AddFields(AcquisitionFields, EditConfig, nameof(ConfigSpectrum.Saturation), nameof(ConfigSpectrum.MaxIntegralTime),
                nameof(ConfigSpectrum.BeginIntegralTime), nameof(ConfigSpectrum.AutoTestTime));
            AddGroup(AcquisitionFields, Properties.Resources.SpectrumAcquisitionSettings, EditConfig.GetDataConfig);
            AddFields(ShutterFields, EditConfig, nameof(ConfigSpectrum.IsShutterEnable));
            ShutterDetails.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(EditConfig.ShutterCfg, showCategoryHeader: false));
            NdFields.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(EditConfig.NDConfig, showCategoryHeader: false));
            FileFields.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(EditConfig.FileServerCfg, showCategoryHeader: false));
        }

        private static void AddFields(Panel panel, object config, params string[] names)
        {
            foreach (string name in names)
            {
                var field = PropertyEditorHelper.GenProperties(config, name, Properties.Resources.ResourceManager);
                field.Margin = new Thickness(0, 3, 0, 5);
                field.MinHeight = 28;
                panel.Children.Add(field);
            }
        }

        private void AddGroup(Panel panel, string title, object config)
        {
            panel.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("SectionTitleStyle"), Margin = new Thickness(0, 20, 0, 8) });
            panel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(config, showCategoryHeader: false));
        }

        private void Config_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConfigSpectrum.SN)) RefreshLicense();
        }

        private async Task LoadChoicesAsync()
        {
            stored = await Task.Run(PhySpectrumStore.Load, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            RefreshChoices();
        }

        private void RefreshChoices()
        {
            string sn = EditConfig.SN ?? string.Empty;
            var catalog = stored.ToDictionary(item => item.SN, StringComparer.OrdinalIgnoreCase);
            foreach (string serial in discoveries.Keys.Concat(string.IsNullOrWhiteSpace(sn) ? Array.Empty<string>() : new[] { sn.Trim() }))
                catalog.TryAdd(serial, new PhySpectrum { SN = serial });
            choices = catalog.Values.Select(item => new PhySpectrum
            {
                SN = item.SN, License = item.License, ResourceId = item.ResourceId,
                DiscoverySource = discoveries.GetValueOrDefault(item.SN) ?? string.Empty
            }).OrderByDescending(item => item.IsDiscovered).ThenBy(item => item.SN, StringComparer.OrdinalIgnoreCase).ToArray();
            SerialChoices.ItemsSource = choices;
            SerialChoices.SetCurrentValue(ComboBox.TextProperty, sn);
            EditConfig.SN = sn;
            RefreshLicense();
        }

        private void RefreshLicense()
        {
            string sn = EditConfig.SN?.Trim() ?? string.Empty;
            var selected = choices.FirstOrDefault(item => string.Equals(item.SN, sn, StringComparison.OrdinalIgnoreCase)) ?? new PhySpectrum { SN = sn };
            LicenseSummary.DataContext = selected;
            ImportButton.IsEnabled = !isBusy && sn.Length > 0;
        }

        private async Task RunAsync(Func<Task> action)
        {
            if (isBusy || closed) return;
            isBusy = true;
            EditorBody.IsEnabled = false;
            OkButton.IsEnabled = false;
            AdvancedButton.IsEnabled = false;
            Progress.Visibility = Visibility.Visible;
            StatusText.Text = Properties.Resources.SpectrumWorking;
            try
            {
                await action();
                if (!closed && StatusText.Text == Properties.Resources.SpectrumWorking) StatusText.Text = string.Empty;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!closed) StatusText.Text = ex.Message; }
            finally
            {
                isBusy = false;
                if (closed) cancellation.Dispose();
                else
                {
                    EditorBody.IsEnabled = true;
                    OkButton.IsEnabled = true;
                    AdvancedButton.IsEnabled = true;
                    Progress.Visibility = Visibility.Collapsed;
                    RefreshLicense();
                }
            }
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            if (!Draft.TryGetPort(out int port))
            {
                StatusText.Text = Properties.Resources.SpectrumEditorInvalidPort;
                return;
            }
            await RunAsync(async () =>
            {
                var results = await Task.Run(() => SpectrumDeviceDiscovery.Discover(port, Spectrometer.CM_Emission_GetAllSN), cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                discoveries.Clear();
                foreach (var result in results.Where(result => result.Error == null))
                    foreach (string sn in result.SerialNumbers)
                    {
                        string source = $"{result.Type} · {(result.ComPort == 0 ? "USB" : $"COM{result.ComPort}")}";
                        discoveries[sn] = discoveries.TryGetValue(sn, out string? previous) ? $"{previous}, {source}" : source;
                    }
                // Discovery is useful even without a configured project or a reachable database.
                RefreshChoices();
                DiscoveryDetails.Text = SpectrumDeviceDiscovery.FormatResults(results);
                DiscoveryExpander.Visibility = Visibility.Visible;
                StatusText.Text = string.Format(Properties.Resources.SpectrumScanComplete, discoveries.Count, results.Count(result => result.Error != null));
            });
            if (!closed && discoveries.Count > 0) SerialChoices.IsDropDownOpen = true;
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            string sn = EditConfig.SN?.Trim() ?? string.Empty;
            if (sn.Length == 0) return;
            var dialog = new OpenFileDialog { Filter = "License (*.zip;*.lic)|*.zip;*.lic", Title = $"{Properties.Resources.SpectrumImportLicense} · {sn}" };
            if (dialog.ShowDialog(this) != true) return;
            await RunAsync(async () =>
            {
                LicenseModel license = await Task.Run(() => SpectrumLicenseUpdateService.ReadFile(dialog.FileName, sn), cancellation.Token);
                await Task.Run(() => PhySpectrumStore.SaveLicense(license), cancellation.Token);
                await LoadChoicesAsync();
                StatusText.Text = $"{sn} · {Properties.Resources.UpdataSucess}";
            });
        }

        private async void ManageLicenses_Click(object sender, RoutedEventArgs e)
        {
            if (!Draft.TryGetPort(out int port))
            {
                StatusText.Text = Properties.Resources.SpectrumEditorInvalidPort;
                return;
            }
            new PhySpectrumManagerWindow(EditConfig.SN, port) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            await RunAsync(LoadChoicesAsync);
        }

        private void Advanced_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            if (!Draft.Prepare(out string error)) { StatusText.Text = error; return; }
            var window = new PropertyEditorWindow(EditConfig, PropertyEditorEditMode.Transactional) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            window.Submitted += (_, _) =>
            {
                Draft.ReloadConnection();
                foreach (Panel panel in new Panel[] { ConnectionFields, SerialFields, CalibrationFields, AcquisitionFields, ShutterFields, ShutterDetails, NdFields, FileFields }) panel.Children.Clear();
                InitializeFields();
                DataContext = null;
                DataContext = this;
                RefreshChoices();
            };
            window.ShowDialog();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
            if (!Draft.TryApply(target, out string error))
            {
                ConnectionTab.IsSelected = true;
                StatusText.Text = error;
                return;
            }
            DialogResult = true;
        }
    }
}
