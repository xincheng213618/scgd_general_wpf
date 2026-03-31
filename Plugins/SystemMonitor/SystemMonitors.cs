using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.CUDA;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.UI.Configs
{
    public class ProcessInfoViewModel
    {
        public int PID { get; set; }
        public string Name { get; set; } = string.Empty;
        public double MemoryMB { get; set; }
        public string MemoryText => $"{MemoryMB:F1} MB";
    }

    public class DriveInfoViewModel : ViewModelBase
    {
        public string Name { get; set; } = string.Empty;
        public string VolumeLabel { get; set; } = string.Empty;
        public string DriveFormat { get; set; } = string.Empty;
        public DriveType DriveType { get; set; }
        public long TotalSize { get; set; }
        public long UsedSpace { get; set; }
        public long FreeSpace { get; set; }
        public double UsagePercent { get; set; }

        public string TotalSizeText => MemorySize.MemorySizeText(TotalSize);
        public string UsedSpaceText => MemorySize.MemorySizeText(UsedSpace);
        public string FreeSpaceText => MemorySize.MemorySizeText(FreeSpace);
        public string UsageText => $"{UsagePercent:F1}%";

        public string UsageColor
        {
            get
            {
                if (UsagePercent > 90) return "#E53935";
                if (UsagePercent > 70) return "#FB8C00";
                return "#43A047";
            }
        }

        public string TrackColor
        {
            get
            {
                if (UsagePercent > 90) return "#FFCDD2";
                if (UsagePercent > 70) return "#FFE0B2";
                return "#C8E6C9";
            }
        }

        public static DriveInfoViewModel FromDriveInfo(DriveInfo drive)
        {
            long used = drive.TotalSize - drive.AvailableFreeSpace;
            double pct = drive.TotalSize > 0 ? (double)used / drive.TotalSize * 100 : 0;
            return new DriveInfoViewModel
            {
                Name = drive.Name.TrimEnd('\\'),
                VolumeLabel = string.IsNullOrEmpty(drive.VolumeLabel) ? SystemMonitor.Properties.Resources.LocalDisk : drive.VolumeLabel,
                DriveFormat = drive.DriveFormat,
                DriveType = drive.DriveType,
                TotalSize = drive.TotalSize,
                UsedSpace = used,
                FreeSpace = drive.AvailableFreeSpace,
                UsagePercent = pct,
            };
        }
    }

    public class NetworkInterfaceViewModel : ViewModelBase
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public NetworkInterfaceType InterfaceType { get; set; }
        public OperationalStatus Status { get; set; }
        public long Speed { get; set; }
        public string SpeedText => Speed >= 1_000_000_000 ? $"{Speed / 1_000_000_000.0:F0} Gbps" : $"{Speed / 1_000_000.0:F0} Mbps";
        public string IPAddress { get; set; } = "N/A";
        public string MacAddress { get; set; } = "N/A";
        public bool IsUp => Status == OperationalStatus.Up;
        public string StatusColor => IsUp ? "#43A047" : "#9E9E9E";
    }

    public class SystemMonitorSetting : ViewModelBase, IConfig
    {
        public int UpdateSpeed
        {
            get => _UpdateSpeed;
            set { if (value >= 100) SetProperty(ref _UpdateSpeed, value); }
        }
        private int _UpdateSpeed = 1000;

        public string DefaultTimeFormat
        {
            get => _DefaultTimeFormat;
            set => SetProperty(ref _DefaultTimeFormat, value);
        }
        private string _DefaultTimeFormat = "yyyy/MM/dd HH:mm:ss";

        public bool IsShowTime
        {
            get => _ShowTime;
            set => SetProperty(ref _ShowTime, value);
        }
        private bool _ShowTime;

        public bool IsShowRAM
        {
            get => _IsShowRAM;
            set => SetProperty(ref _IsShowRAM, value);
        }
        private bool _IsShowRAM;

        public bool IsShowCPU
        {
            get => _IsShowCPU;
            set => SetProperty(ref _IsShowCPU, value);
        }
        private bool _IsShowCPU;

        public bool IsShowUptime
        {
            get => _IsShowUptime;
            set => SetProperty(ref _IsShowUptime, value);
        }
        private bool _IsShowUptime;

        public bool IsShowDisk
        {
            get => _IsShowDisk;
            set => SetProperty(ref _IsShowDisk, value);
        }
        private bool _IsShowDisk;
    }

    public class SystemMonitors : ViewModelBase, IDisposable
    {
        private static SystemMonitors? _instance;
        private static readonly object _locker = new();
        public static SystemMonitors GetInstance() { lock (_locker) { return _instance ??= new SystemMonitors(); } }

        private bool _isDisposed;
        private readonly object _perfCounterLock = new();
        private bool _perfCounterReady;
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _cpuThisCounter;
        private PerformanceCounter? _ramCounter;
        private PerformanceCounter? _ramThisCounter;
        private Timer? _timer;
        private readonly double _totalRAMGB;

        public SystemMonitorSetting Config { get; set; }
        public RelayCommand ClearCacheCommand { get; set; }
        public RelayCommand RefreshDrivesCommand { get; set; }
        public RelayCommand RefreshNetworkCommand { get; set; }

        // 磁盘
        public ObservableCollection<DriveInfoViewModel> Drives { get; set; } = new();

        // 网络
        public ObservableCollection<NetworkInterfaceViewModel> NetworkInterfaces { get; set; } = new();

        // CPU
        public double CPUPercent { get => _CPUPercent; set { _CPUPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(CPUText)); } }
        private double _CPUPercent;

        public double CPUThisPercent { get => _CPUThisPercent; set { _CPUThisPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(CPUThisText)); } }
        private double _CPUThisPercent;

        public string CPUText => $"{_CPUPercent:F1}%";
        public string CPUThisText => $"{_CPUThisPercent:F1}%";

        // RAM
        public double RAMPercent { get => _RAMPercent; set { _RAMPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(RAMText)); } }
        private double _RAMPercent;

        public double RAMThisPercent { get => _RAMThisPercent; set { _RAMThisPercent = value; OnPropertyChanged(); } }
        private double _RAMThisPercent;

        public string RAMThis { get => _RAMThis; set { _RAMThis = value; OnPropertyChanged(); } }
        private string _RAMThis = string.Empty;

        public string MemoryThis { get => _MemoryThis; set { _MemoryThis = value; OnPropertyChanged(); } }
        private string _MemoryThis = string.Empty;

        public string RAMText => $"{_RAMPercent:F1}%";
        public string TotalRAMText => $"{_totalRAMGB:F1} GB";

        // 时间
        public string Time { get => _Time; set { _Time = value; OnPropertyChanged(); } }
        private string _Time = string.Empty;

        public string GetUptime { get => _GetUptime; set { _GetUptime = value; OnPropertyChanged(); } }
        private string _GetUptime = string.Empty;

        // 系统信息 (静态)
        public string OSVersion { get; }
        public string MachineName { get; }
        public string ProcessorName { get; }
        public int ProcessorCount { get; }

        // GPU
        public string GPUName { get; }
        public bool HasGPU { get; }
        public string GPUMemoryText { get; }

        // 磁盘汇总
        public string TotalDiskSpace { get => _TotalDiskSpace; set { _TotalDiskSpace = value; OnPropertyChanged(); } }
        private string _TotalDiskSpace = string.Empty;

        public string TotalDiskFree { get => _TotalDiskFree; set { _TotalDiskFree = value; OnPropertyChanged(); } }
        private string _TotalDiskFree = string.Empty;

        // 缓存大小
        public string CacheSize { get => _CacheSize; set { _CacheSize = value; OnPropertyChanged(); } }
        private string _CacheSize = string.Empty;

        // 兼容旧代码
        public string ProcessorTotal { get => _ProcessorTotal; set { _ProcessorTotal = value; OnPropertyChanged(); } }
        private string _ProcessorTotal = string.Empty;

        public string CurrentDiskTotalSize { get => _CurrentDiskTotalSize; set { _CurrentDiskTotalSize = value; OnPropertyChanged(); } }
        private string _CurrentDiskTotalSize = string.Empty;

        public string MemoryAvailable { get => _MemoryAvailable; set { _MemoryAvailable = value; OnPropertyChanged(); } }
        private string _MemoryAvailable = string.Empty;

        // 状态栏显示文本 (更醒目)
        public string CPUStatusText { get => _CPUStatusText; set { _CPUStatusText = value; OnPropertyChanged(); } }
        private string _CPUStatusText = "CPU 0.0%";

        public string RAMStatusText { get => _RAMStatusText; set { _RAMStatusText = value; OnPropertyChanged(); } }
        private string _RAMStatusText = "RAM 0.0%";

        // 系统启动时间
        public string OSUptime { get => _OSUptime; set { _OSUptime = value; OnPropertyChanged(); } }
        private string _OSUptime = string.Empty;

        public string OSBootTimeText { get; }

        // 运行时环境
        public string DotNetVersion { get; }
        public string Architecture { get; }
        public string UserName { get; }
        public int CurrentPID { get; }
        public string ProcessStartTimeText { get; }
        public string ScreenInfo { get; }

        // 当前进程动态信息
        public int ThreadCount { get => _ThreadCount; set { _ThreadCount = value; OnPropertyChanged(); } }
        private int _ThreadCount;

        public int HandleCount { get => _HandleCount; set { _HandleCount = value; OnPropertyChanged(); } }
        private int _HandleCount;

        // 进程列表
        public ObservableCollection<ProcessInfoViewModel> TopProcesses { get; set; } = new();
        public RelayCommand RefreshProcessesCommand { get; set; }

        public int UpdateSpeed
        {
            get => Config.UpdateSpeed;
            set
            {
                if (value != Config.UpdateSpeed && value > 0)
                {
                    Config.UpdateSpeed = value;
                    OnPropertyChanged();
                    _timer?.Dispose();
                    _timer = new Timer(OnTimerTick, null, 0, value);
                }
            }
        }

        public SystemMonitors()
        {
            Config = ConfigService.Instance.GetRequiredService<SystemMonitorSetting>();
            _totalRAMGB = (double)Common.NativeMethods.PerformanceInfo.GetTotalMemoryInMiB() / 1024;

            MachineName = Environment.MachineName;
            ProcessorCount = Environment.ProcessorCount;
            OSVersion = Environment.OSVersion.VersionString;
            ProcessorName = SystemHelper.LocalCpuInfo;

            // 运行时环境信息
            DotNetVersion = RuntimeInformation.FrameworkDescription;
            Architecture = RuntimeInformation.OSArchitecture.ToString();
            UserName = Environment.UserName;

            var currentProcess = Process.GetCurrentProcess();
            CurrentPID = currentProcess.Id;
            ProcessStartTimeText = currentProcess.StartTime.ToString("yyyy-MM-dd HH:mm:ss");

            var osUptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            var bootTime = DateTime.Now - osUptime;
            OSBootTimeText = bootTime.ToString("yyyy-MM-dd HH:mm:ss");

            ScreenInfo = $"{(int)SystemParameters.PrimaryScreenWidth} × {(int)SystemParameters.PrimaryScreenHeight}";

            var cudaConfig = ConfigCuda.Instance;
            if (cudaConfig.IsCudaSupported && cudaConfig.DeviceNames?.Length > 0)
            {
                GPUName = string.Join(", ", cudaConfig.DeviceNames);
                HasGPU = true;
                if (cudaConfig.TotalMemories?.Length > 0)
                    GPUMemoryText = MemorySize.MemorySizeText((long)cudaConfig.TotalMemories[0]);
                else
                    GPUMemoryText = string.Empty;
            }
            else
            {
                GPUName = SystemMonitor.Properties.Resources.NotDetected;
                HasGPU = false;
                GPUMemoryText = string.Empty;
            }

            ClearCacheCommand = new RelayCommand(a => ClearCache());
            RefreshDrivesCommand = new RelayCommand(a => LoadDrives());
            RefreshNetworkCommand = new RelayCommand(a => LoadNetworkInterfaces());
            RefreshProcessesCommand = new RelayCommand(a => LoadTopProcesses());

            Task.Run(InitPerformanceCounters);
            LoadDrives();
            LoadNetworkInterfaces();
            UpdateCacheSize();
            LoadTopProcesses();

            _timer = new Timer(OnTimerTick, null, 0, Config.UpdateSpeed);
        }

        private void InitPerformanceCounters()
        {
            try
            {
                lock (_perfCounterLock)
                {
                    string processName = Process.GetCurrentProcess().ProcessName;
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    _cpuThisCounter = new PerformanceCounter("Process", "% Processor Time", processName);
                    _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                    _ramThisCounter = new PerformanceCounter("Process", "Working Set - Private", processName);
                    _cpuCounter.NextValue();
                    _cpuThisCounter.NextValue();
                    _ramCounter.NextValue();
                    _ramThisCounter.NextValue();
                    _perfCounterReady = true;
                }
            }
            catch (Exception ex)
            {
                _perfCounterReady = false;
                Debug.WriteLine($"Failed to init perf counters: {ex.Message}");
            }
        }

        public void LoadDrives()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Drives.Clear();
                long totalSize = 0, totalFree = 0;
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    Drives.Add(DriveInfoViewModel.FromDriveInfo(drive));
                    totalSize += drive.TotalSize;
                    totalFree += drive.AvailableFreeSpace;
                }
                TotalDiskSpace = MemorySize.MemorySizeText(totalSize);
                TotalDiskFree = MemorySize.MemorySizeText(totalFree);
            });
        }

        public void LoadNetworkInterfaces()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                NetworkInterfaces.Clear();
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                             && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel))
                {
                    var props = nic.GetIPProperties();
                    var ipv4 = props.UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    var mac = nic.GetPhysicalAddress();
                    NetworkInterfaces.Add(new NetworkInterfaceViewModel
                    {
                        Name = nic.Name,
                        Description = nic.Description,
                        InterfaceType = nic.NetworkInterfaceType,
                        Status = nic.OperationalStatus,
                        Speed = nic.Speed,
                        IPAddress = ipv4?.Address.ToString() ?? "N/A",
                        MacAddress = mac != null ? string.Join(":", mac.GetAddressBytes().Select(b => b.ToString("X2"))) : "N/A",
                    });
                }
            });
        }

        public void LoadTopProcesses()
        {
            Task.Run(() =>
            {
                try
                {
                    var processes = Process.GetProcesses()
                        .Select(p =>
                        {
                            try { return new { p.Id, p.ProcessName, Memory = p.WorkingSet64 / 1024.0 / 1024.0 }; }
                            catch { return null; }
                        })
                        .Where(p => p != null)
                        .OrderByDescending(p => p!.Memory)
                        .Take(10)
                        .Select(p => new ProcessInfoViewModel { PID = p!.Id, Name = p.ProcessName, MemoryMB = p.Memory })
                        .ToList();

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        TopProcesses.Clear();
                        foreach (var p in processes)
                            TopProcesses.Add(p);
                    });
                }
                catch { }
            });
        }

        public void UpdateCacheSize()
        {
            Task.Run(() =>
            {
                long size = 0;
                try
                {
                    if (Directory.Exists(Environments.DirAppData))
                        size += MemorySize.GetDirectoryLength(Environments.DirAppData);
                    if (Environments.DirLog != null)
                    {
                        string? logDir = Path.GetDirectoryName(Environments.DirLog);
                        if (logDir != null && Directory.Exists(logDir))
                            size += MemorySize.GetDirectoryLength(logDir);
                    }
                }
                catch { }
                Application.Current?.Dispatcher.Invoke(() => CacheSize = MemorySize.MemorySizeText(size));
            });
        }

        private void OnTimerTick(object? state)
        {
            if (_isDisposed) return;

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                // 应用运行时长
                var ts = SystemHelper.GetUptime();
                GetUptime = ts.Days == 0
                    ? $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}"
                    : $"{ts.Days}天 {ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";

                // 系统运行时长
                var osTs = TimeSpan.FromMilliseconds(Environment.TickCount64);
                OSUptime = osTs.Days > 0
                    ? $"{osTs.Days}天 {osTs.Hours:00}:{osTs.Minutes:00}:{osTs.Seconds:00}"
                    : $"{osTs.Hours:00}:{osTs.Minutes:00}:{osTs.Seconds:00}";

                // 当前进程信息
                try
                {
                    var proc = Process.GetCurrentProcess();
                    ThreadCount = proc.Threads.Count;
                    HandleCount = proc.HandleCount;
                }
                catch { }
            });

            if (!_perfCounterReady) return;

            try
            {
                lock (_perfCounterLock)
                {
                    if (_cpuCounter == null || _ramCounter == null) return;

                    float availableRAM = _ramCounter.NextValue();
                    float usedRAMGB = (float)_totalRAMGB - (availableRAM / 1024);
                    RAMPercent = (usedRAMGB / _totalRAMGB) * 100;

                    float curRAM = _ramThisCounter!.NextValue() / 1024 / 1024;
                    RAMThisPercent = (curRAM / 1024 / _totalRAMGB) * 100;
                    RAMThis = curRAM.ToString("f1") + " MB";
                    MemoryThis = curRAM.ToString("f1") + " MB / " + _totalRAMGB.ToString("f1") + " GB";
                    MemoryAvailable = availableRAM.ToString("f0") + " MB";

                    CPUPercent = _cpuCounter.NextValue();
                    CPUThisPercent = _cpuThisCounter!.NextValue();
                    ProcessorTotal = CPUPercent.ToString("f1") + "%";

                    // 状态栏醒目文本
                    CPUStatusText = "CPU " + CPUPercent.ToString("f1") + "%";
                    RAMStatusText = "RAM " + RAMPercent.ToString("f1") + "%";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Perf counter error: {ex.Message}");
            }

            try
            {
                // 始终更新时间，可见性由状态栏配置控制
                Time = DateTime.Now.ToString(Config.DefaultTimeFormat);
            }
            catch { }
        }

        public void ClearCache()
        {
            int deletedCount = 0;
            try
            {
                if (Directory.Exists(Environments.DirAppData))
                {
                    foreach (var item in new DirectoryInfo(Environments.DirAppData).GetFiles())
                    {
                        try { item.Delete(); deletedCount++; }
                        catch { }
                    }
                }
                if (Environments.DirLog != null)
                {
                    string? logDir = Path.GetDirectoryName(Environments.DirLog);
                    if (logDir != null && Directory.Exists(logDir))
                    {
                        foreach (var item in new DirectoryInfo(logDir).GetFiles())
                        {
                            try { item.Delete(); deletedCount++; }
                            catch { }
                        }
                    }
                }
                MessageBox.Show($"清除成功，删除了 {deletedCount} 个文件");
                UpdateCacheSize();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清除失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _timer?.Dispose();
            _timer = null;
            lock (_perfCounterLock)
            {
                _cpuCounter?.Dispose();
                _cpuThisCounter?.Dispose();
                _ramCounter?.Dispose();
                _ramThisCounter?.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }
}
