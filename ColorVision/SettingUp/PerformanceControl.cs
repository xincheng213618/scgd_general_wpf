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
        private PerformanceCounter ProcessThis;

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
                    ProcessThis = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
                    RAM = new PerformanceCounter("Memory", "Available MBytes");

                    RAMThis = new PerformanceCounter("Process", "Working Set - Private", Process.GetCurrentProcess().ProcessName);
                    CPU = new PerformanceCounter("Processor", "% Processor Time", "_Total");
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
                MemoryThis = (RAMThis.NextValue() / 1024 / 1024).ToString("f1") + "MB" + "/" + RAMAL.ToString("f1") + "GB";
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


        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

}
