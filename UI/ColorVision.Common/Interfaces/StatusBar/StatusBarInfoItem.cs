using System.ComponentModel;

namespace ColorVision.UI
{
    /// <summary>
    /// 状态栏信息项，用于封装动态状态信息
    /// </summary>
    public class StatusBarInfoItem : INotifyPropertyChanged
    {
        /// <summary>
        /// 信息项的唯一标识，用于在状态栏中定位和更新
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 显示标签，如 "行:列:", "通道:", "名称:"
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// 信息值
        /// </summary>
        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }
        private string _value;

        /// <summary>
        /// 完整的显示文本（标签 + 值）
        /// </summary>
        public string DisplayText => string.IsNullOrEmpty(Label) ? Value : $"{Label} {Value}";

        /// <summary>
        /// 是否显示此信息项
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChanged(nameof(IsVisible));
                }
            }
        }
        private bool _isVisible = true;

        /// <summary>
        /// 排序顺序，数值越小越靠前
        /// </summary>
        public int Order { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
