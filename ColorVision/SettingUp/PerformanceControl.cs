using ColorVision.MVVM;
using ColorVision.MySql;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision
{
    public class PerformanceControl : ViewModelBase, IDisposable
    {
        private static PerformanceControl _instance;
        private static readonly object _locker = new();
        public static PerformanceControl GetInstance() { lock (_locker) { return _instance ??= new PerformanceControl(); } }

        private bool PerformanceCounterIsOpen;
        private PerformanceCounter CPU;
        private PerformanceCounter CPUThis;

        private double RAMAL = (double)NativeMethods.PerformanceInfo.GetTotalMemoryInMiB() / 1024;
        private PerformanceCounter RAM;
        private PerformanceCounter RAMThis;

        private Timer timer;
        private int _UpdateSpeed = 1000;

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

        public PerformanceControl()
        {
            Task.Run(() => 
            {
                try
                {
                    CPU = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    CPUThis = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
                    RAM = new PerformanceCounter("Memory", "Available MBytes");

                    RAMThis = new PerformanceCounter("Process", "Working Set - Private", Process.GetCurrentProcess().ProcessName);

                    PerformanceCounterIsOpen = true;
                }
                catch
                {
                    PerformanceCounterIsOpen = false;
                }
            });
            timer = new Timer(TimeRun, null, 0, UpdateSpeed);
        }


        private void TimeRun(object? state)
        {
            if (PerformanceCounterIsOpen)
            {


                RAMPercent = 100- RAM.NextValue() / 1024 / RAMAL * 100;
                RAMThisPercent = RAMThis.NextValue() / 1024 / 1024 / 1024 / RAMAL * 100;

                CPUThisPercent = CPUThis.NextValue();
                CPUPercent = CPU.NextValue();


                float curRAM = RAMThis.NextValue() / 1024 / 1024;

                MemoryThis = curRAM.ToString("f1") + "MB" + "/" + RAMAL.ToString("f1") + "GB";
                ProcessorTotal = CPU.NextValue().ToString("f1") + "%";
                Time = DateTime.Now.ToString("MM月dd日 HH:mm:ss");
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
        private string _ProcessorTotal = String.Empty;


        /// <summary>
        /// 内存获取
        /// </summary>
        public string MemoryAvailable { get => _MemoryAvailable; set { _MemoryAvailable = value; NotifyPropertyChanged(); } }
        private string _MemoryAvailable = String.Empty;

        /// <summary>
        /// 当前软件占用内存
        /// </summary>
        public string MemoryThis { get => _MemoryThis; set { _MemoryThis = value; NotifyPropertyChanged(); } }
        private string _MemoryThis = String.Empty;

        public double RAMPercent { get => _RAMPercent; set { _RAMPercent = value; NotifyPropertyChanged(); } }
        private double _RAMPercent;

        public double RAMThisPercent { get => _RAMThisPercent; set { _RAMThisPercent = value; NotifyPropertyChanged(); } }
        private double _RAMThisPercent;

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
