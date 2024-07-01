using ColorVision.Common.MVVM;
using System.Collections.Generic;
using System;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    public class CameraVideoConfig : ViewModelBase
    {
        /// <summary>
        /// IP地址
        /// </summary>
        public string Host { get => _Host; set { _Host = value; NotifyPropertyChanged(); } }
        private string _Host = "127.0.0.1";

        public bool IsEnableResize { get => _IsEnableResize; set { _IsEnableResize = value; NotifyPropertyChanged(); } }
        private bool _IsEnableResize;

        public float ResizeRatio { get => _ResizeRatio; set { _ResizeRatio = value; NotifyPropertyChanged(); } }
        private float _ResizeRatio;

        /// <summary>
        /// 端口地址
        /// </summary>
        public int Port
        {
            get => _Port; set
            {
                _Port = value <= 0 ? 0 : value >= 65535 ? 65535 : value;
                NotifyPropertyChanged();
            }
        }
        private int _Port = 9002;

        public long Capacity
        {
            get => _Capacity;
            set
            {
                if (_Capacity != value)
                {
                    _Capacity = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(CapacityText));
                    NotifyPropertyChanged(nameof(CapacityInput));
                }
            }
        }
        private long _Capacity = 1073741824;

        public string CapacityText => MemorySizeText(_Capacity);

        public string CapacityInput
        {
            get => MemorySizeText(_Capacity);
            set
            {
                if (TryParseMemorySize(value, out long parsedValue))
                {
                    Capacity = parsedValue;
                }
            }
        }

        public static string MemorySizeText(long memorySize)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;
            const long TB = GB * 1024;
            const long PB = TB * 1024;

            var units = new[]
            {
            Tuple.Create(PB, "PB"),
            Tuple.Create(TB, "TB"),
            Tuple.Create(GB, "GB"),
            Tuple.Create(MB, "MB"),
            Tuple.Create(KB, "kB"),
            Tuple.Create(1L, "Byte")
        };

            foreach (var unit in units)
            {
                if (memorySize >= unit.Item1)
                {
                    double value = (double)memorySize / unit.Item1;
                    if (memorySize < unit.Item1 * 10)
                    {
                        return $"{value:F1} {unit.Item2}";
                    }
                    return $"{(long)value} {unit.Item2}";
                }
            }
            return "0 Byte";
        }

        public static bool TryParseMemorySize(string input, out long memorySize)
        {
            input = input.Trim().ToUpperInvariant();
            memorySize = 0;

            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            var units = new Dictionary<string, long>
        {
            { "PB", 1024L * 1024 * 1024 * 1024 * 1024 },
            { "TB", 1024L * 1024 * 1024 * 1024 },
            { "GB", 1024L * 1024 * 1024 },
            { "MB", 1024L * 1024 },
            { "KB", 1024L },
            { "B", 1L }
        };

            foreach (var unit in units)
            {
                if (input.EndsWith(unit.Key))
                {
                    if (double.TryParse(input.Substring(0, input.Length - unit.Key.Length), out double value))
                    {
                        memorySize = (long)(value * unit.Value);
                        return true;
                    }
                }
            }

            return long.TryParse(input, out memorySize); // Try to parse as bytes if no unit is found
        }

    }
}
