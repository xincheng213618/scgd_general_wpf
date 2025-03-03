using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace ColorVision.ImageEditor
{
    public enum CVCIETYpe
    {
        Circle,
        Rect
    }

    public class ImageViewConfig:ViewModelBase
    {
        [JsonIgnore]
        public Dictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?>();

        public Dictionary<string, object?> EditProperties { get; set; } = new Dictionary<string, object?>();

        public void AddProperties(string Key,object? Value)
        {
            if (!Properties.TryAdd(Key, Value))
                Properties[Key] = Value;
        }
        public T? GetProperties<T>(string Key)
        {
            if (Properties.TryGetValue(Key, out var value))
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }
            }
            return default;
        }
        private  static string FormatValue(object? value)
        {
            if (value is IEnumerable enumerable && value is not string)
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                {
                    items.Add(item?.ToString() ?? "null");
                }
                return $"[{string.Join(", ", items)}]";
            }
            return value?.ToString() ?? "null";
        }
        public string GetPropertyString()
        {
            var sb = new StringBuilder();
            foreach (var item in Properties)
            {
                sb.AppendLine($"{item.Key}:{FormatValue(item.Value)}");
            }
            return sb.ToString();

        }
        public double MaxZoom { get => _MaxZoom; set { _MaxZoom = value; NotifyPropertyChanged(); } }
        private double _MaxZoom = 10;
        public double MinZoom { get => _MinZoom; set { _MinZoom = value; NotifyPropertyChanged(); } }
        private double _MinZoom = 0.01;

        public int CVCIENum { get => _CVCIENum; set { _CVCIENum = value; NotifyPropertyChanged(); } }
        private int _CVCIENum = 1;
        public double PoiCircleRadius { get => _PoiCircleRadius; set { _PoiCircleRadius = value; NotifyPropertyChanged(); } }
        private double _PoiCircleRadius = 1;

        public CVCIETYpe CVCIETYpe { get => _CVCIETYpe; set { _CVCIETYpe = value; NotifyPropertyChanged(); } }
        private CVCIETYpe _CVCIETYpe;

        [JsonIgnore]
        public string FilePath { get => _FilePath; set { _FilePath = value; NotifyPropertyChanged(); } }
        private string _FilePath;
        [JsonIgnore]
        public bool ConvertXYZhandleOnce { get; set; }

        [JsonIgnore]
        public IntPtr ConvertXYZhandle { get; set; } = Tool.GenerateRandomIntPtr();

        [JsonIgnore]
        public int Channel { get => _Channel; set { _Channel = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsChannel1));} }
        private int _Channel;

        [JsonIgnore]
        public bool IsChannel1 => Channel == 1;

        public int Ochannel { get; set; }

        public bool IsShowLoadImage { get => _IsShowLoadImage; set { _IsShowLoadImage = value; NotifyPropertyChanged(); } }
        private bool _IsShowLoadImage = true;

        public event EventHandler ColormapTypesChanged;

        public ColormapTypes ColormapTypes { get => _ColormapTypes; set { _ColormapTypes = value; NotifyPropertyChanged(); ColormapTypesChanged?.Invoke(this, new EventArgs()); } }
        private ColormapTypes _ColormapTypes = ColormapTypes.COLORMAP_JET;

        public bool IsLayoutUpdated{ get => _IsLayoutUpdated; set { _IsLayoutUpdated = value; NotifyPropertyChanged(); } }
        private bool _IsLayoutUpdated = true;

        public bool IsShowColorBar { get => _IsShowColorBar; set { _IsShowColorBar = value; NotifyPropertyChanged(); } }
        private bool _IsShowColorBar = true;

        public event EventHandler BalanceChanged;


        public double RedBalance { get => _RedBalance; set { _RedBalance = value; NotifyPropertyChanged(); BalanceChanged?.Invoke(this, new EventArgs()); } }
        private double _RedBalance = 1;

        public double GreenBalance { get => _GreenBalance; set { _GreenBalance = value; NotifyPropertyChanged(); BalanceChanged?.Invoke(this, new EventArgs()); } }
        private double _GreenBalance = 1;

        public double BlueBalance { get => _BlueBalance; set { _BlueBalance = value; NotifyPropertyChanged(); BalanceChanged?.Invoke(this, new EventArgs()); } }
        private double _BlueBalance = 1;

    }
}
