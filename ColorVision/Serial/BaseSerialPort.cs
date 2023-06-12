#pragma warning disable CS8618
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Serial
{
    /// <summary>
    /// 串口通信基础类
    /// </summary>
    public class BaseSerialPort : INotifyPropertyChanged
    {
        internal SerialPort serialPort { get; set; }

        internal Timer timer;

        public event PropertyChangedEventHandler? PropertyChanged;
        internal void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool _IsOpen;
        /// <summary>
        /// 是否打开
        /// </summary>
        public bool IsOpen
        {
            get => _IsOpen;
            set { _IsOpen = value; NotifyPropertyChanged(); }
        }
    }
}
