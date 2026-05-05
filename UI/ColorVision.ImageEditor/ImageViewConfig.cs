using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ColorVision.ImageEditor
{

    public interface IImageEditorConfig
    {

    }

    public class ImageViewConfig:ViewModelBase
    {
        public RelayCommand ClearCommand { get; set; }

        public ImageViewConfig()
        {
            Configs = new Dictionary<Type, IImageEditorConfig>();

            ClearCommand = new RelayCommand(o => Cleared?.Invoke(this,new EventArgs()));    
        }

        public Dictionary<Type, IImageEditorConfig> Configs { get; set; }

        public T GetRequiredService<T>() where T : IImageEditorConfig
        {
            var type = typeof(T);

            if (Configs.TryGetValue(type, out var service))
            {
                return (T)service;
            }

            if (Activator.CreateInstance(type) is IImageEditorConfig defaultConfig)
            {
                Configs[type] = defaultConfig;
            }
            // 此处递归调用是为了确保缓存和异常处理逻辑一致
            return GetRequiredService<T>();
        }
        public event EventHandler Cleared;
        [JsonIgnore]
        public Dictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?>();

        public void ClearProperties()
        {
            FilePath = string.Empty;
            Properties.Clear();
            Cleared?.Invoke(this, new EventArgs());
        }



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


        [JsonIgnore]
        public string FilePath { get => GetProperties<string>("FilePath"); set { AddProperties("FilePath", value) ; OnPropertyChanged(); } }


        public event EventHandler<bool> LayoutUpdatedChanged;
        [DisplayName("自动刷新")]
        public bool IsLayoutUpdated
        {
            get => _IsLayoutUpdated;
            set
            {
                if (_IsLayoutUpdated == value) return;
                _IsLayoutUpdated = value;
                OnPropertyChanged();
                LayoutUpdatedChanged?.Invoke(this, _IsLayoutUpdated);
            }
        }
        private bool _IsLayoutUpdated = true;

        public event EventHandler<double> DrawingTextFontSizeChanged;
        [DisplayName("绘制文字大小")]
        public double DrawingTextFontSize { get => _DrawingTextFontSize; set { _DrawingTextFontSize = Math.Max(0, value); OnPropertyChanged(); DrawingTextFontSizeChanged?.Invoke(this, _DrawingTextFontSize); } }
        private double _DrawingTextFontSize;


        public event EventHandler<bool> ShowTextChanged;
        [DisplayName("显示文字")]
        public bool IsShowText { get => _IsShowText; set { _IsShowText = value; OnPropertyChanged(); ShowTextChanged?.Invoke(this, _IsShowText); } }
        private bool _IsShowText = true;

        public event EventHandler<bool> ShowMsgChanged;
        [DisplayName("显示消息")]
        public bool IsShowMsg { get => _IsShowMsg; set { _IsShowMsg = value; OnPropertyChanged(); ShowMsgChanged?.Invoke(this, _IsShowMsg); } }
        private bool _IsShowMsg = true;

        // Toolbar visibility properties
        public bool IsToolBarAlVisible { get => _IsToolBarAlVisible; set { _IsToolBarAlVisible = value; OnPropertyChanged(); } }
        private bool _IsToolBarAlVisible = true;

        public bool IsToolBarDrawVisible { get => _IsToolBarDrawVisible; set { _IsToolBarDrawVisible = value; OnPropertyChanged(); } }
        private bool _IsToolBarDrawVisible;

        public bool IsToolBarTopVisible { get => _IsToolBarTopVisible; set { _IsToolBarTopVisible = value; OnPropertyChanged(); } }
        private bool _IsToolBarTopVisible = true;

        public bool IsToolBarLeftVisible { get => _IsToolBarLeftVisible; set { _IsToolBarLeftVisible = value; OnPropertyChanged(); } }
        private bool _IsToolBarLeftVisible = true;

        public bool IsToolBarRightVisible { get => _IsToolBarRightVisible; set { _IsToolBarRightVisible = value; OnPropertyChanged(); } }
        private bool _IsToolBarRightVisible = true;

    }
}
