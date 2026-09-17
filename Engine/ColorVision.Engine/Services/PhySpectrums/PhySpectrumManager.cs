using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using cvColorVision;
using log4net;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace ColorVision.Engine.Services.PhySpectrums
{
    public sealed class PhySpectrumManager : ViewModelBase, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PhySpectrumManager));
        private readonly SpectrumLicenseUpdateService licenseService = new();
        private readonly Dictionary<string, string> discoveries = new(StringComparer.OrdinalIgnoreCase);
        private readonly CancellationTokenSource cancellation = new();
        private readonly string? initialSerial;
        private bool disposed;

        public ObservableCollection<PhySpectrum> Spectrums { get; } = new();
        public ICollectionView FilteredSpectrums { get; }
        public RelayCommand ScanCommand { get; }
        public RelayCommand UpdateCommand { get; }
        public RelayCommand ImportCommand { get; }
        public RelayCommand ImportAllCommand { get; }
        public RelayCommand CopyLicenseCommand { get; }
        public RelayCommand ExportLicenseCommand { get; }
        public RelayCommand OpenDriverToolCommand { get; }
        public RelayCommand OpenLogCommand { get; }
        public RelayCommand OpenCorrectionCommand { get; }

        public PhySpectrumManager(string? initialSerial = null, int comPort = 0)
        {
            this.initialSerial = initialSerial;
            ComPort = comPort;
            FilteredSpectrums = CollectionViewSource.GetDefaultView(Spectrums);
            FilteredSpectrums.Filter = o => o is PhySpectrum item && (string.IsNullOrWhiteSpace(SearchText)
                || item.SN.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)
                || item.DisplayModel.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase));
            ScanCommand = new RelayCommand(async _ => await RunAsync(ScanAsync), _ => !IsBusy && ComPort >= 0 && ComPort <= 256);
            UpdateCommand = new RelayCommand(async _ => await RunAsync(() => UpdateLicenseAsync(SelectedSpectrum!.SN), silent: true), _ => !IsBusy && SelectedSpectrum != null);
            ImportCommand = new RelayCommand(_ => ImportLicense(), _ => !IsBusy && SelectedSpectrum != null);
            ImportAllCommand = new RelayCommand(_ => ImportLicenses(), _ => !IsBusy);
            CopyLicenseCommand = new RelayCommand(async _ => await RunAsync(() => { Clipboard.SetText(SelectedSpectrum!.License!.LicenseValue!); return Task.CompletedTask; }, silent: true), _ => CanExportLicense);
            ExportLicenseCommand = new RelayCommand(_ => ExportLicense(), _ => CanExportLicense);
            OpenDriverToolCommand = new RelayCommand(_ => DeviceSpectrum.OpenSpectrumDriverTool(), _ => !IsBusy);
            OpenLogCommand = new RelayCommand(_ => DeviceSpectrum.OpenSpectrumLog(), _ => !IsBusy);
            OpenCorrectionCommand = new RelayCommand(_ => OpenCorrection(), _ => !IsBusy && SelectedCorrectionDevice != null);
        }

        public PhySpectrum? SelectedSpectrum { get => selectedSpectrum; set { selectedSpectrum = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); RefreshCorrectionDevices(); RefreshCommands(); } }
        private PhySpectrum? selectedSpectrum;
        public bool HasSelection => SelectedSpectrum != null;
        public IReadOnlyList<DeviceSpectrum> CorrectionDevices { get; private set; } = Array.Empty<DeviceSpectrum>();
        public bool HasMultipleCorrectionDevices => CorrectionDevices.Count > 1;
        public string CorrectionHint => CorrectionDevices.Count == 0 ? Properties.Resources.SpectrumCorrectionNeedsDevice : Properties.Resources.SpectrumCorrectionHint;
        public DeviceSpectrum? SelectedCorrectionDevice { get => selectedCorrectionDevice; set { selectedCorrectionDevice = value; OnPropertyChanged(); OpenCorrectionCommand?.RaiseCanExecuteChanged(); } }
        private DeviceSpectrum? selectedCorrectionDevice;
        public string SearchText { get => searchText; set { searchText = value; OnPropertyChanged(); FilteredSpectrums.Refresh(); } }
        private string searchText = string.Empty;
        public int ComPort { get; }
        public bool IsBusy { get => isBusy; private set { isBusy = value; OnPropertyChanged(); RefreshCommands(); } }
        private bool isBusy;
        public bool ShowProgress { get => showProgress; private set { showProgress = value; OnPropertyChanged(); } }
        private bool showProgress;
        private bool CanExportLicense => !IsBusy && !string.IsNullOrWhiteSpace(SelectedSpectrum?.License?.LicenseValue);
        public string StatusText { get => statusText; private set { statusText = value; OnPropertyChanged(); } }
        private string statusText = string.Empty;
        public string DiscoveryDetails { get => discoveryDetails; private set { discoveryDetails = value; OnPropertyChanged(); } }
        private string discoveryDetails = string.Empty;
        public string SummaryText => string.Format(Properties.Resources.SpectrumManagerSummary, Spectrums.Count, Spectrums.Count(s => s.IsDiscovered));

        public Task RefreshAsync() => RunAsync(async () => { await ReloadAsync(); StatusText = string.Empty; });
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            cancellation.Cancel();
            if (!IsBusy) cancellation.Dispose();
            GC.SuppressFinalize(this);
        }

        private void RefreshCommands()
        {
            foreach (var command in new[] { ScanCommand, UpdateCommand, ImportCommand, ImportAllCommand, CopyLicenseCommand, ExportLicenseCommand, OpenDriverToolCommand, OpenLogCommand, OpenCorrectionCommand })
                command?.RaiseCanExecuteChanged();
        }

        internal static IReadOnlyList<DeviceSpectrum> MatchCorrectionDevices(IEnumerable<DeviceSpectrum> devices, string? serial) =>
            string.IsNullOrWhiteSpace(serial) ? Array.Empty<DeviceSpectrum>() : devices
                .Where(device => string.Equals(device.Config.SN?.Trim(), serial.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();

        private void RefreshCorrectionDevices()
        {
            // Only use already-loaded services. Opening a physical catalog must not create runtime devices.
            CorrectionDevices = MatchCorrectionDevices(ServiceManager.Current?.DeviceServices.OfType<DeviceSpectrum>() ?? Enumerable.Empty<DeviceSpectrum>(), SelectedSpectrum?.SN);
            OnPropertyChanged(nameof(CorrectionDevices));
            OnPropertyChanged(nameof(HasMultipleCorrectionDevices));
            OnPropertyChanged(nameof(CorrectionHint));
            SelectedCorrectionDevice = CorrectionDevices.Contains(SelectedCorrectionDevice!) ? SelectedCorrectionDevice
                : CorrectionDevices.Count == 1 ? CorrectionDevices[0] : null;
        }

        private void OpenCorrection()
        {
            // Re-resolve after a service reload or configuration edit before forwarding the existing command.
            RefreshCorrectionDevices();
            var command = SelectedCorrectionDevice?.OpenSpectrumCorrectionCommand;
            if (command?.CanExecute(null) == true) command.Execute(null);
        }

        private async Task RunAsync(Func<Task> action, bool silent = false)
        {
            if (IsBusy || disposed) return;
            IsBusy = true;
            ShowProgress = !silent;
            if (!silent) StatusText = Properties.Resources.SpectrumWorking;
            try { await action(); }
            catch (OperationCanceledException) { if (!silent) StatusText = Properties.Resources.Cancel; }
            catch (Exception ex) { Log.Error("Physical spectrum operation failed", ex); if (!silent) StatusText = ex.Message; }
            finally
            {
                IsBusy = false;
                ShowProgress = false;
                OnPropertyChanged(nameof(SummaryText));
                if (disposed) cancellation.Dispose();
            }
        }

        private async Task ReloadAsync(string? selectSerial = null)
        {
            selectSerial ??= SelectedSpectrum?.SN ?? initialSerial;
            var stored = (await Task.Run(PhySpectrumStore.Load, cancellation.Token)).ToDictionary(s => s.SN, StringComparer.OrdinalIgnoreCase);
            cancellation.Token.ThrowIfCancellationRequested();
            foreach (string sn in discoveries.Keys.Concat(string.IsNullOrWhiteSpace(initialSerial) ? Array.Empty<string>() : new[] { initialSerial.Trim() }))
                stored.TryAdd(sn, new PhySpectrum { SN = sn });
            Spectrums.Clear();
            foreach (var entry in stored.Values.OrderByDescending(s => discoveries.ContainsKey(s.SN)).ThenBy(s => s.SN, StringComparer.OrdinalIgnoreCase))
                Spectrums.Add(new PhySpectrum { SN = entry.SN, ResourceId = entry.ResourceId, License = entry.License,
                    DiscoverySource = discoveries.GetValueOrDefault(entry.SN) ?? string.Empty });
            SelectedSpectrum = Spectrums.FirstOrDefault(s => string.Equals(s.SN, selectSerial, StringComparison.OrdinalIgnoreCase)) ?? Spectrums.FirstOrDefault();
            OnPropertyChanged(nameof(SummaryText));
            RefreshCommands();
        }

        private async Task ScanAsync()
        {
            int port = ComPort;
            var results = await Task.Run(() => SpectrumDeviceDiscovery.Discover(port, Spectrometer.CM_Emission_GetAllSN), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            discoveries.Clear();
            foreach (var result in results.Where(r => r.Error == null))
                foreach (string sn in result.SerialNumbers)
                {
                    string source = $"{result.Type} · {(result.ComPort == 0 ? "USB" : $"COM{result.ComPort}")}";
                    discoveries[sn] = discoveries.TryGetValue(sn, out string? previous) ? $"{previous}, {source}" : source;
                }
            DiscoveryDetails = SpectrumDeviceDiscovery.FormatResults(results);
            await Task.Run(() =>
            {
                foreach (string sn in discoveries.Keys)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    PhySpectrumStore.Register(sn);
                }
            }, cancellation.Token);
            await ReloadAsync();
            StatusText = string.Format(Properties.Resources.SpectrumScanComplete, discoveries.Count, results.Count(r => r.Error != null));
        }

        private async Task UpdateLicenseAsync(string sn)
        {
            LicenseModel license = await licenseService.DownloadAsync(sn, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            await Task.Run(() => PhySpectrumStore.SaveLicense(license), cancellation.Token);
            Log.Info($"Spectrum license updated: {sn}");
            await ReloadAsync();
        }

        private async void ImportLicense()
        {
            string? sn = SelectedSpectrum?.SN;
            if (sn == null) return;
            var dialog = new OpenFileDialog { Filter = "License (*.zip;*.lic)|*.zip;*.lic", Title = $"{Properties.Resources.SpectrumImportLicense} · {sn}" };
            if (dialog.ShowDialog() != true) return;
            await RunAsync(async () =>
            {
                var license = await Task.Run(() => SpectrumLicenseUpdateService.ReadFile(dialog.FileName, sn), cancellation.Token);
                await Task.Run(() => PhySpectrumStore.SaveLicense(license), cancellation.Token);
                await ReloadAsync(sn);
                StatusText = $"{sn} · {Properties.Resources.UpdataSucess}";
            });
        }

        private async void ExportLicense()
        {
            var item = SelectedSpectrum;
            if (string.IsNullOrWhiteSpace(item?.License?.LicenseValue)) return;
            var dialog = new SaveFileDialog { Filter = "License (*.lic)|*.lic", FileName = item.SN + ".lic", Title = Properties.Resources.ExportLicense };
            if (dialog.ShowDialog() != true) return;
            await RunAsync(() => File.WriteAllTextAsync(dialog.FileName, item.License.LicenseValue, cancellation.Token), silent: true);
        }

        private async void ImportLicenses()
        {
            var dialog = new OpenFileDialog { Filter = "License (*.zip;*.lic)|*.zip;*.lic", Title = Properties.Resources.LicenseImport, Multiselect = true };
            if (dialog.ShowDialog() != true) return;
            await RunAsync(async () =>
            {
                // Validate every selected file before writing the first license.
                var licenses = await Task.Run(() => SpectrumLicenseUpdateService.ReadFiles(dialog.FileNames), cancellation.Token);
                foreach (var license in licenses)
                    await Task.Run(() => PhySpectrumStore.SaveLicense(license), cancellation.Token);
                SearchText = string.Empty;
                await ReloadAsync(licenses.FirstOrDefault()?.MacAddress);
                StatusText = Properties.Resources.UpdataSucess;
            });
        }
    }
}
