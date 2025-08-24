using ColorVision.Common.MVVM;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.UI.Configs
{
    public class SystemMonitorSetting : ViewModelBase, IConfig
    {
        public int UpdateSpeed { get => _UpdateSpeed; set { _UpdateSpeed = value; OnPropertyChanged(); } }
        private int _UpdateSpeed = 1000;

        public string DefaultTimeFormat { get => _DefaultTimeFormat; set { _DefaultTimeFormat = value; OnPropertyChanged(); } }
        private string _DefaultTimeFormat = "yyyy/MM/dd HH:mm:ss";

        public bool IsShowTime { get => _ShowTime; set { _ShowTime = value; OnPropertyChanged(); } }
        private bool _ShowTime;

        public bool IsShowRAM { get => _IsShowRAM; set { _IsShowRAM = value; OnPropertyChanged(); } }
        private bool _IsShowRAM;

    }

    public class SystemMonitors : ViewModelBase, IDisposable
    {
        private static SystemMonitors _instance;
        private static readonly object _locker = new();
        public static SystemMonitors GetInstance() { lock (_locker) { return _instance ??= new SystemMonitors(); } }


        public RelayCommand ClearCacheCommand { get; set; }


        public static void ClearCache()
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(Environments.DirAppData);
            foreach (var item in directoryInfo.GetFiles())
            {
                try
                {
                    item.Delete();
                }
                catch
                {

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
                    }
                    catch
                    {

                    }
                }
            }
            MessageBox.Show("清除成功");
        }




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
                    Config.UpdateSpeed = value; OnPropertyChanged();
                    timer?.Dispose();
                    timer = new Timer(TimeRun, null, 0, value);
                }
            }
        }

        public SystemMonitorSetting Config { get; set; }


        public SystemMonitors()
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
            ClearCacheCommand = new RelayCommand(a => ClearCache());

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
                    //RAMPercent = 100 - PCRAM.NextValue() / 1024 / RAMAL * 100;
                    //RAMThisPercent = PCRAMThis.NextValue() / 1024 / 1024 / 1024 / RAMAL * 100;

                    //CPUThisPercent = PCCPUThis.NextValue();
                    //CPUPercent = PCCPU.NextValue();

                    //float curRAM = PCRAMThis.NextValue() / 1024 / 1024;
                    //RAMThis = curRAM.ToString("f1") + "MB";
                    //MemoryThis = curRAM.ToString("f1") + "MB" + "/" + RAMAL.ToString("f1") + "GB";
                    //ProcessorTotal = PCCPU.NextValue().ToString("f1") + "%";
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
        public string CurrentDiskTotalSize { get => _CurrentDiskTotalSize; set { _CurrentDiskTotalSize = value; OnPropertyChanged(); } }
        private string _CurrentDiskTotalSize = string.Empty;


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


        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

}
