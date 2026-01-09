using ColorVision.Common.MVVM;
using ColorVision.UI.CUDA;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.UI.Configs
{
    /// <summary>
    /// Configuration settings for system monitoring
    /// </summary>
    public class SystemMonitorSetting : ViewModelBase, IConfig
    {
        private int _UpdateSpeed = 1000;
        private string _DefaultTimeFormat = "yyyy/MM/dd HH:mm:ss";
        private bool _ShowTime;
        private bool _IsShowRAM;

        /// <summary>
        /// Update interval in milliseconds (must be >= 100)
        /// </summary>
        public int UpdateSpeed 
        { 
            get => _UpdateSpeed; 
            set 
            { 
                if (value >= 100) // Minimum 100ms to prevent excessive CPU usage
                {
                    SetProperty(ref _UpdateSpeed, value);
                }
            } 
        }

        /// <summary>
        /// Time format string for status bar display
        /// </summary>
        public string DefaultTimeFormat 
        { 
            get => _DefaultTimeFormat; 
            set => SetProperty(ref _DefaultTimeFormat, value); 
        }

        /// <summary>
        /// Whether to show time in status bar
        /// </summary>
        public bool IsShowTime 
        { 
            get => _ShowTime; 
            set => SetProperty(ref _ShowTime, value); 
        }

        /// <summary>
        /// Whether to show RAM usage in status bar
        /// </summary>
        public bool IsShowRAM 
        { 
            get => _IsShowRAM; 
            set => SetProperty(ref _IsShowRAM, value); 
        }
    }

    /// <summary>
    /// System monitor for tracking CPU, RAM, disk usage and system time
    /// </summary>
    public class SystemMonitors : ViewModelBase, IDisposable
    {
        private static SystemMonitors _instance;
        private static readonly object _locker = new();
        public static SystemMonitors GetInstance() { lock (_locker) { return _instance ??= new SystemMonitors(); } }


        public RelayCommand ClearCacheCommand { get; set; }


        /// <summary>
        /// Clears application cache and log files
        /// </summary>
        public static void ClearCache()
        {
            int deletedCount = 0;
            try
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(Environments.DirAppData);
                foreach (var item in directoryInfo.GetFiles())
                {
                    try
                    {
                        item.Delete();
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with other files
                        System.Diagnostics.Debug.WriteLine($"Failed to delete {item.Name}: {ex.Message}");
                    }
                }

                if (Environments.DirLog != null)
                {
                    DirectoryInfo logDir = new DirectoryInfo(Environments.DirLog);
                    foreach (var item in logDir.GetFiles())
                    {
                        try
                        {
                            item.Delete();
                            deletedCount++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to delete log {item.Name}: {ex.Message}");
                        }
                    }
                }
                MessageBox.Show($"清除成功，删除了 {deletedCount} 个文件");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清除失败: {ex.Message}");
            }
        }




        private bool _isDisposed;
        private readonly object _perfCounterLock = new object();
        
        private bool PerformanceCounterIsOpen;
        private PerformanceCounter? PCCPU;
        private PerformanceCounter? PCCPUThis;

        private readonly double RAMAL = (double)Common.NativeMethods.PerformanceInfo.GetTotalMemoryInMiB() / 1024;
        private PerformanceCounter? PCRAM;
        private PerformanceCounter? PCRAMThis;

        private Timer? timer;

        /// <summary>
        /// Gets or sets the update speed in milliseconds
        /// </summary>
        public int UpdateSpeed
        {
            get => Config.UpdateSpeed; 
            set
            {
                if (value != Config.UpdateSpeed && value > 0)
                {
                    Config.UpdateSpeed = value; 
                    OnPropertyChanged();
                    
                    // Restart timer with new interval
                    timer?.Dispose();
                    timer = new Timer(TimeRun, null, 0, value);
                }
            }
        }

        public SystemMonitorSetting Config { get; set; }


        public SystemMonitors()
        {
            Config = ConfigService.Instance.GetRequiredService<SystemMonitorSetting>();
            
            // Initialize performance counters asynchronously
            Task.Run(() =>
            {
                try
                {
                    lock (_perfCounterLock)
                    {
                        PCCPU = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                        PCCPUThis = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
                        PCRAM = new PerformanceCounter("Memory", "Available MBytes");
                        PCRAMThis = new PerformanceCounter("Process", "Working Set - Private", Process.GetCurrentProcess().ProcessName);
                        
                        // First call to initialize counters
                        PCCPU.NextValue();
                        PCCPUThis.NextValue();
                        PCRAM.NextValue();
                        PCRAMThis.NextValue();
                        
                        PerformanceCounterIsOpen = true;
                    }
                }
                catch (Exception ex)
                {
                    PerformanceCounterIsOpen = false;
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize performance counters: {ex.Message}");
                }
            });
            
            timer = new Timer(TimeRun, null, 0, UpdateSpeed);
            ClearCacheCommand = new RelayCommand(a => ClearCache());

            // Load drive information
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (var item in allDrives)
            {
                if (item.IsReady)
                {
                    DriveInfos.Add(item);
                }
            }
        }


        public ObservableCollection<DriveInfo> DriveInfos { get; set; } = new ObservableCollection<DriveInfo>();

        /// <summary>
        /// Timer callback to update monitoring data
        /// </summary>
        private void TimeRun(object? state)
        {
            if (_isDisposed) return;
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                var ts = SystemHelper.GetUptime();
                if(ts.Days == 0)
                {
                    GetUptime = $"正常运行时间 {ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
                }
                else
                {
                    GetUptime = $"正常运行时间 {ts.Days}:{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
                }
            });


            if (PerformanceCounterIsOpen)
            {
                try
                {
                    lock (_perfCounterLock)
                    {
                        if (PCCPU != null && PCRAM != null && PCCPUThis != null && PCRAMThis != null)
                        {
                            // Update performance metrics
                            float availableRAM = PCRAM.NextValue();
                            float totalRAMGB = (float)RAMAL;
                            float usedRAMGB = totalRAMGB - (availableRAM / 1024);
                            
                            RAMPercent = (usedRAMGB / totalRAMGB) * 100;
                            
                            float curRAM = PCRAMThis.NextValue() / 1024 / 1024;
                            RAMThisPercent = (curRAM / 1024 / totalRAMGB) * 100;
                            RAMThis = curRAM.ToString("f1") + " MB";
                            MemoryThis = curRAM.ToString("f1") + " MB / " + totalRAMGB.ToString("f1") + " GB";
                            
                            CPUPercent = PCCPU.NextValue();
                            CPUThisPercent = PCCPUThis.NextValue();
                            ProcessorTotal = CPUPercent.ToString("f1") + "%";
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating performance counters: {ex.Message}");
                }

                try
                {
                    if (Config.IsShowTime)
                    {
                        Time = DateTime.Now.ToString(Config.DefaultTimeFormat);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating time: {ex.Message}");
                }
            }
        }
        /// <summary>
        /// 当前分区硬盘大小
        /// </summary>
        public string CurrentDiskTotalSize { get => _CurrentDiskTotalSize; set { _CurrentDiskTotalSize = value; OnPropertyChanged(); } }
        private string _CurrentDiskTotalSize = string.Empty;

        public string GetUptime { get => _GetUptime; set { _GetUptime = value; OnPropertyChanged(); } }
        private string _GetUptime = string.Empty;

        public string Time { get => _Time; set { _Time = value; OnPropertyChanged(); } }
        private string _Time = string.Empty;


        /// <summary>
        /// 总处理器占用
        /// </summary>
        public string ProcessorTotal { get => _ProcessorTotal; set { _ProcessorTotal = value; OnPropertyChanged(); } }
        private string _ProcessorTotal = string.Empty;


        /// <summary>
        /// 内存获取
        /// </summary>
        public string MemoryAvailable { get => _MemoryAvailable; set { _MemoryAvailable = value; OnPropertyChanged(); } }
        private string _MemoryAvailable = string.Empty;

        /// <summary>
        /// 当前软件占用内存
        /// </summary>
        public string MemoryThis { get => _MemoryThis; set { _MemoryThis = value; OnPropertyChanged(); } }
        private string _MemoryThis = string.Empty;

        public double RAMPercent { get => _RAMPercent; set { _RAMPercent = value; OnPropertyChanged(); } }
        private double _RAMPercent;

        public double RAMThisPercent { get => _RAMThisPercent; set { _RAMThisPercent = value; OnPropertyChanged(); } }
        private double _RAMThisPercent;

        public string RAMThis { get => _RAMThis; set { _RAMThis = value; OnPropertyChanged(); } }
        private string _RAMThis = string.Empty;

        public double CPUPercent { get => _CPUPercent; set { _CPUPercent = value; OnPropertyChanged(); } }
        private double _CPUPercent;

        public double CPUThisPercent { get => _CPUThisPercent; set { _CPUThisPercent = value; OnPropertyChanged(); } }
        private double _CPUThisPercent;


        /// <summary>
        /// Disposes resources used by the system monitor
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            
            _isDisposed = true;
            
            // Dispose timer
            timer?.Dispose();
            timer = null;
            
            // Dispose performance counters
            lock (_perfCounterLock)
            {
                PCCPU?.Dispose();
                PCCPUThis?.Dispose();
                PCRAM?.Dispose();
                PCRAMThis?.Dispose();
                
                PCCPU = null;
                PCCPUThis = null;
                PCRAM = null;
                PCRAMThis = null;
            }
            
            GC.SuppressFinalize(this);
        }
    }

}
