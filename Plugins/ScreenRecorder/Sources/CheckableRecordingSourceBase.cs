using ScreenRecorderLib;
using System.ComponentModel;

namespace ScreenRecorder.Sources
{
    /// <summary>
    /// 可勾选录制源的基类，提供公共属性和方法实现
    /// </summary>
    public abstract class CheckableRecordingSourceBase : ICheckableRecordingSource
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性变更通知
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region ICheckableRecordingSource Implementation

        private bool _isSelected;
        /// <summary>
        /// 是否被选中用于录制
        /// </summary>
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        private bool _isCheckable = true;
        /// <summary>
        /// 是否可被勾选
        /// </summary>
        public bool IsCheckable
        {
            get { return _isCheckable; }
            set
            {
                if (_isCheckable != value)
                {
                    _isCheckable = value;
                    OnPropertyChanged(nameof(IsCheckable));
                }
            }
        }

        private bool _isCustomPositionEnabled;
        /// <summary>
        /// 是否启用自定义位置
        /// </summary>
        public bool IsCustomPositionEnabled
        {
            get { return _isCustomPositionEnabled; }
            set
            {
                if (_isCustomPositionEnabled != value)
                {
                    _isCustomPositionEnabled = value;
                    OnPropertyChanged(nameof(IsCustomPositionEnabled));
                }
            }
        }

        private bool _isCustomOutputSizeEnabled;
        /// <summary>
        /// 是否启用自定义输出尺寸
        /// </summary>
        public bool IsCustomOutputSizeEnabled
        {
            get { return _isCustomOutputSizeEnabled; }
            set
            {
                if (_isCustomOutputSizeEnabled != value)
                {
                    _isCustomOutputSizeEnabled = value;
                    OnPropertyChanged(nameof(IsCustomOutputSizeEnabled));
                }
            }
        }

        private bool _isCustomOutputSourceRectEnabled;
        /// <summary>
        /// 是否启用自定义源区域
        /// </summary>
        public bool IsCustomOutputSourceRectEnabled
        {
            get { return _isCustomOutputSourceRectEnabled; }
            set
            {
                if (_isCustomOutputSourceRectEnabled != value)
                {
                    _isCustomOutputSourceRectEnabled = value;
                    OnPropertyChanged(nameof(IsCustomOutputSourceRectEnabled));
                }
            }
        }

        /// <summary>
        /// 输出尺寸
        /// </summary>
        public abstract ScreenSize OutputSize { get; set; }

        /// <summary>
        /// 输出位置
        /// </summary>
        public abstract ScreenPoint Position { get; set; }

        /// <summary>
        /// 源区域
        /// </summary>
        public abstract ScreenRect SourceRect { get; set; }

        /// <summary>
        /// 是否启用视频捕获
        /// </summary>
        public abstract bool IsVideoCaptureEnabled { get; set; }

        /// <summary>
        /// 更新屏幕坐标
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="size">尺寸</param>
        public virtual void UpdateScreenCoordinates(ScreenPoint position, ScreenSize size)
        {
            if (!IsCustomOutputSourceRectEnabled)
            {
                SourceRect = new ScreenRect(0, 0, size.Width, size.Height);
            }
            if (!IsCustomOutputSizeEnabled)
            {
                OutputSize = size;
            }
            if (!IsCustomPositionEnabled)
            {
                Position = position;
            }
        }

        #endregion
    }
}
