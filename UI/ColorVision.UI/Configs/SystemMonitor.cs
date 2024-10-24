using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
namespace ColorVision.UI.Configs
{
    public class SystemMonitorSetting : ViewModelBase, IConfig
    {
        public int UpdateSpeed { get => _UpdateSpeed; set { _UpdateSpeed = value; NotifyPropertyChanged(); } }
        private int _UpdateSpeed = 1000;

        public string DefaultTimeFormat { get => _DefaultTimeFormat; set { _DefaultTimeFormat = value; NotifyPropertyChanged(); } }
        private string _DefaultTimeFormat = "yyyy/MM/dd HH:mm:ss";

        public bool IsShowTime { get => _ShowTime; set { _ShowTime = value; NotifyPropertyChanged(); } }
        private bool _ShowTime;

        public bool IsShowRAM { get => _IsShowRAM; set { _IsShowRAM = value; NotifyPropertyChanged(); } }
        private bool _IsShowRAM;

    }

    public class SystemMonitor : ViewModelBase, IDisposable
    {
        private static SystemMonitor _instance;
        private static readonly object _locker = new();
        public static SystemMonitor GetInstance() { lock (_locker) { return _instance ??= new SystemMonitor(); } }

        private bool PerformanceCounterIsOpen;
        private PerformanceCounter PCCPU;
        private PerformanceCounter PCCPUThis;

        private double RAMAL = (double)Common.NativeMethods.PerformanceInfo.GetTotalMemoryInMiB() / 1024;
        private PerformanceCounter PCRAM;
        private PerformanceCounter PCRAMThis;

        private Timer timer;

        public int UpdateSpeed
        {
            get => Config.UpdateSpeed; set
            {
                if (value != Config.UpdateSpeed)
                {
                    Config.UpdateSpeed = value; NotifyPropertyChanged();
                    timer?.Dispose();
                    timer = new Timer(TimeRun, null, 0, value);
                }
            }
        }

        public SystemMonitorSetting Config { get; set; }


        public SystemMonitor()
        {
            Config = ConfigService.Instance.GetRequiredService<SystemMonitorSetting>();
            Task.Run(() =>
            {
                try
                {
                    PCCPU = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    PCCPUThis = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
                    PCRAM = new PerformanceCounter("Memory", "Available MBytes");
                    PCRAMThis = new PerformanceCounter("Process", "Working Set - Private", Process.GetCurrentProcess().ProcessName);
                    PerformanceCounterIsOpen = true;
                }
                catch
                {
                    PerformanceCounterIsOpen = false;
                }
            });
            timer = new Timer(TimeRun, null, 0, UpdateSpeed);

            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (var item in allDrives)
            {
                DriveInfos.Add(item);
            }
        }
        public ObservableCollection<DriveInfo> DriveInfos { get; set; } = new ObservableCollection<DriveInfo>();

        private void TimeRun(object? state)
        {
            if (PerformanceCounterIsOpen)
            {
                try
                {
                    RAMPercent = 100 - PCRAM.NextValue() / 1024 / RAMAL * 100;
                    RAMThisPercent = PCRAMThis.NextValue() / 1024 / 1024 / 1024 / RAMAL * 100;

                    CPUThisPercent = PCCPUThis.NextValue();
                    CPUPercent = PCCPU.NextValue();

                    float curRAM = PCRAMThis.NextValue() / 1024 / 1024;
                    RAMThis = curRAM.ToString("f1") + "MB";
                    MemoryThis = curRAM.ToString("f1") + "MB" + "/" + RAMAL.ToString("f1") + "GB";
                    ProcessorTotal = PCCPU.NextValue().ToString("f1") + "%";
                }
                catch
                {

                }

                try
                {
                    if (Config.IsShowTime)
                        Time = DateTime.Now.ToString(Config.DefaultTimeFormat);
                }
                catch
                {

                }
            }
        }
        /// <summary>
        /// 当前分区硬盘大小
        /// </summary>
        public string CurrentDiskTotalSize { get => _CurrentDiskTotalSize; set { _CurrentDiskTotalSize = value; NotifyPropertyChanged(); } }
        private string _CurrentDiskTotalSize = string.Empty;


        public string Time { get => _Time; set { _Time = value; NotifyPropertyChanged(); } }
        private string _Time = string.Empty;


        /// <summary>
        /// 总处理器占用
        /// </summary>
        public string ProcessorTotal { get => _ProcessorTotal; set { _ProcessorTotal = value; NotifyPropertyChanged(); } }
        private string _ProcessorTotal = string.Empty;


        /// <summary>
        /// 内存获取
        /// </summary>
        public string MemoryAvailable { get => _MemoryAvailable; set { _MemoryAvailable = value; NotifyPropertyChanged(); } }
        private string _MemoryAvailable = string.Empty;

        /// <summary>
        /// 当前软件占用内存
        /// </summary>
        public string MemoryThis { get => _MemoryThis; set { _MemoryThis = value; NotifyPropertyChanged(); } }
        private string _MemoryThis = string.Empty;

        public double RAMPercent { get => _RAMPercent; set { _RAMPercent = value; NotifyPropertyChanged(); } }
        private double _RAMPercent;

        public double RAMThisPercent { get => _RAMThisPercent; set { _RAMThisPercent = value; NotifyPropertyChanged(); } }
        private double _RAMThisPercent;

        public string RAMThis { get => _RAMThis; set { _RAMThis = value; NotifyPropertyChanged(); } }
        private string _RAMThis = string.Empty;

        public double CPUPercent { get => _CPUPercent; set { _CPUPercent = value; NotifyPropertyChanged(); } }
        private double _CPUPercent;

        public double CPUThisPercent { get => _CPUThisPercent; set { _CPUThisPercent = value; NotifyPropertyChanged(); } }
        private double _CPUThisPercent;


        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

}
