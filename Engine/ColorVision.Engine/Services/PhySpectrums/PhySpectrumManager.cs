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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ScanCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand RegisterCommand { get; }
        public RelayCommand UpdateCommand { get; }
        public RelayCommand UpdateAllCommand { get; }
        public RelayCommand ImportCommand { get; }

        public PhySpectrumManager(string? initialSerial = null, int comPort = 0)
        {
            this.initialSerial = initialSerial;
            ComPort = comPort;
            FilteredSpectrums = CollectionViewSource.GetDefaultView(Spectrums);
            FilteredSpectrums.Filter = o => o is PhySpectrum item && (string.IsNullOrWhiteSpace(SearchText)
                || item.SN.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)
                || item.DisplayModel.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase));
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync(), _ => !IsBusy);
            ScanCommand = new RelayCommand(async _ => await RunAsync(ScanAsync), _ => !IsBusy && ComPort >= 0 && ComPort <= 256);
            AddCommand = new RelayCommand(async _ => await RegisterAsync(NewSerial), _ => !IsBusy && !string.IsNullOrWhiteSpace(NewSerial));
            RegisterCommand = new RelayCommand(async _ => await RegisterAsync(SelectedSpectrum!.SN), _ => !IsBusy && SelectedSpectrum is { IsRegistered: false });
            UpdateCommand = new RelayCommand(async _ => await RunAsync(() => UpdateLicensesAsync(new[] { SelectedSpectrum! })), _ => !IsBusy && SelectedSpectrum != null);
            UpdateAllCommand = new RelayCommand(async _ => await RunAsync(() => UpdateLicensesAsync(Spectrums.ToArray())), _ => !IsBusy && Spectrums.Count > 0);
            ImportCommand = new RelayCommand(_ => ImportLicense(), _ => !IsBusy && SelectedSpectrum != null);
        }

        public PhySpectrum? SelectedSpectrum { get => selectedSpectrum; set { selectedSpectrum = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); RefreshCommands(); } }
        private PhySpectrum? selectedSpectrum;
        public bool HasSelection => SelectedSpectrum != null;
        public string SearchText { get => searchText; set { searchText = value; OnPropertyChanged(); FilteredSpectrums.Refresh(); } }
        private string searchText = string.Empty;
        public string NewSerial { get => newSerial; set { newSerial = value; OnPropertyChanged(); RefreshCommands(); } }
        private string newSerial = string.Empty;
        public int ComPort { get => comPort; set { comPort = value; OnPropertyChanged(); RefreshCommands(); } }
        private int comPort;
        public bool IsBusy { get => isBusy; private set { isBusy = value; OnPropertyChanged(); RefreshCommands(); } }
        private bool isBusy;
        public string StatusText { get => statusText; private set { statusText = value; OnPropertyChanged(); } }
        private string statusText = Properties.Resources.SpectrumManagerHint;
        public string DiscoveryDetails { get => discoveryDetails; private set { discoveryDetails = value; OnPropertyChanged(); } }
        private string discoveryDetails = string.Empty;
        public string SummaryText => string.Format(Properties.Resources.SpectrumManagerSummary, Spectrums.Count, Spectrums.Count(s => s.IsDiscovered), Spectrums.Count(s => s.NeedsAttention));

        public Task RefreshAsync() => RunAsync(async () => { await ReloadAsync(); StatusText = Properties.Resources.SpectrumManagerHint; });
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
            foreach (var command in new[] { RefreshCommand, ScanCommand, AddCommand, RegisterCommand, UpdateCommand, UpdateAllCommand, ImportCommand })
                command?.RaiseCanExecuteChanged();
        }

        private async Task RunAsync(Func<Task> action)
        {
            if (IsBusy || disposed) return;
            IsBusy = true;
            StatusText = Properties.Resources.SpectrumWorking;
            try { await action(); }
            catch (OperationCanceledException) { StatusText = Properties.Resources.Cancel; }
            catch (Exception ex) { Log.Error("Physical spectrum operation failed", ex); StatusText = ex.Message; }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(SummaryText));
                if (disposed) cancellation.Dispose();
            }
        }

        private async Task ReloadAsync(string? selectSerial = null)
        {
            selectSerial ??= SelectedSpectrum?.SN ?? initialSerial;
            var previousResults = Spectrums.ToDictionary(s => s.SN, s => s.OperationResult, StringComparer.OrdinalIgnoreCase);
            var stored = (await Task.Run(PhySpectrumStore.Load, cancellation.Token)).ToDictionary(s => s.SN, StringComparer.OrdinalIgnoreCase);
            cancellation.Token.ThrowIfCancellationRequested();
            foreach (string sn in discoveries.Keys.Concat(string.IsNullOrWhiteSpace(initialSerial) ? Array.Empty<string>() : new[] { initialSerial.Trim() }))
                stored.TryAdd(sn, new PhySpectrum { SN = sn });
            Spectrums.Clear();
            foreach (var entry in stored.Values.OrderByDescending(s => discoveries.ContainsKey(s.SN)).ThenBy(s => s.SN, StringComparer.OrdinalIgnoreCase))
                Spectrums.Add(new PhySpectrum { SN = entry.SN, ResourceId = entry.ResourceId, License = entry.License,
                    DiscoverySource = discoveries.GetValueOrDefault(entry.SN) ?? string.Empty, OperationResult = previousResults.GetValueOrDefault(entry.SN) ?? string.Empty });
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
            await ReloadAsync();
            StatusText = string.Format(Properties.Resources.SpectrumScanComplete, discoveries.Count, results.Count(r => r.Error != null));
        }

        private Task RegisterAsync(string serial) => RunAsync(async () =>
        {
            string sn = PhySpectrumStore.NormalizeSerial(serial);
            await Task.Run(() => PhySpectrumStore.Register(sn), cancellation.Token);
            SearchText = string.Empty;
            await ReloadAsync(sn);
            NewSerial = string.Empty;
            StatusText = $"{sn} · {Properties.Resources.SpectrumRegistered}";
        });

        private async Task UpdateLicensesAsync(IReadOnlyList<PhySpectrum> items)
        {
            int succeeded = 0;
            foreach (var item in items)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                StatusText = $"{Properties.Resources.SpectrumUpdateLicense} · {item.SN}";
                try
                {
                    LicenseModel license = await licenseService.DownloadAsync(item.SN, cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    await Task.Run(() => PhySpectrumStore.SaveLicense(license), cancellation.Token);
                    item.OperationResult = Properties.Resources.UpdataSucess;
                    succeeded++;
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    Log.Error($"Spectrum license update failed: {item.SN}", ex);
                    item.OperationResult = ex.Message;
                }
            }
            string? failure = items.Count == 1 && succeeded == 0 ? items[0].OperationResult : null;
            await ReloadAsync();
            StatusText = failure ?? string.Format(Properties.Resources.SpectrumUpdateSummary, succeeded, items.Count - succeeded);
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
                SelectedSpectrum!.OperationResult = Properties.Resources.UpdataSucess;
                StatusText = $"{sn} · {Properties.Resources.UpdataSucess}";
            });
        }
    }
}
