using ColorVision.MVVM;
using System;
using System.IO;
using System.Threading;

namespace ColorVision.Info
{
    public class PerformanceSetting : ViewModelBase
    {
        private Timer timer;
        private PerformanceCounterHelper Perf;
        public int UpdateSpeed
        {
            get => _UpdateSpeed; set
            {
                if (value != _UpdateSpeed)
                {
                    _UpdateSpeed = value; NotifyPropertyChanged();
                    timer?.Dispose();
                    timer = new Timer(TimeRun, null, 0, value);
                }
            }    
        }
        public int _UpdateSpeed = 1000;


        public PerformanceSetting()
        {
            Perf = PerformanceCounterHelper.GetInstance();
            timer = new Timer(TimeRun, null, 0, UpdateSpeed);
            OSInfo = Environment.OSVersion.Version.Build >= 22000 ? Environment.OSVersion.ToString().Replace("10.", "11.") : Environment.OSVersion.ToString() + " " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit");
        }

        private void TimeRun(object state)
        {
            if (Perf.IsOpen)
            {
                MemorytThis = (Perf.RAMThis.NextValue() / 1024 / 1024).ToString("f1") + "MB" + "/" + Perf.RAMAL.ToString("f1") + "GB";
                ProcessorTotal = Perf.CPU.NextValue().ToString("f1") + "%";
                Time = DateTime.Now.ToString("MM月dd日 HH:mm:ss");
            }
        }
        public string OSInfo { get; set; }

        public DriveInfo CurrentDrive
        {
            get { return _CurrentDrive; }
            set
            {
                _CurrentDrive = value; NotifyPropertyChanged();
                CurrentDiskTotalSize = (CurrentDrive.TotalSize / (1024 * 1024 * 1024)).ToString() + "GB";
                DiskUse = (CurrentDrive.TotalFreeSpace / (1024 * 1024 * 1024)).ToString() + "GB";
            }
        }
        private DriveInfo _CurrentDrive = null;

        /// <summary>
        /// 当前分区硬盘大小
        /// </summary>
        public string CurrentDiskTotalSize { get => _CurrentDiskTotalSize; set { _CurrentDiskTotalSize = value; NotifyPropertyChanged(); } }
        private string _CurrentDiskTotalSize = string.Empty;


        public string Time { get => _Time; set { _Time = value; NotifyPropertyChanged(); } }
        private string _Time = string.Empty;

        /// <summary>
        /// 是否显示硬盘不足警告
        /// </summary>
        public bool IsDiskLackWarning { get => _IsDiskLackWarning; set { _IsDiskLackWarning = value; NotifyPropertyChanged(); } }
        private bool _IsDiskLackWarning = false;

        /// <summary>
        /// 是否显示硬盘不足警告
        /// </summary>
        public bool IsShowDiskLackWarning { get => _IsShowDiskLackWarning; set { _IsShowDiskLackWarning = value; NotifyPropertyChanged(); } }
        private bool _IsShowDiskLackWarning;

        /// <summary>
        /// 是否显示内存不足警告
        /// </summary>
        public bool IsMemoryLackWarning { get => _IsMemoryLackWarning; set { _IsMemoryLackWarning = value; NotifyPropertyChanged(); } }
        private bool _IsMemoryLackWarning = false;

        /// <summary>
        /// 总处理器占用
        /// </summary>
        public string ProcessorTotal { get => _ProcessorTotal; set { _ProcessorTotal = value; NotifyPropertyChanged(); } }
        private string _ProcessorTotal = String.Empty;


        /// <summary>
        /// 内存获取
        /// </summary>
        public string MemoryAvailable { get => _MemoryAvailable; set { _MemoryAvailable = value; NotifyPropertyChanged(); } }
        private string _MemoryAvailable = String.Empty;

        /// <summary>
        /// 当前软件占用内存
        /// </summary>
        public string MemorytThis { get => _MemorytThis; set { _MemorytThis = value; NotifyPropertyChanged(); } }
        private string _MemorytThis = String.Empty;

        /// <summary>
        /// 硬盘使用
        /// </summary>
        public string DiskUse { get => _DiskUse;set { _DiskUse = value; NotifyPropertyChanged(); }}
        private string _DiskUse = String.Empty;
    }

}
