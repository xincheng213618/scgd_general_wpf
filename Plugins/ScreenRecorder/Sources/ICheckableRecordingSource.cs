using ScreenRecorderLib;
using System.ComponentModel;

namespace ScreenRecorder.Sources
{
    /// <summary>
    /// 可勾选录制源接口，定义录制源的基本属性和行为
    /// </summary>
    public interface ICheckableRecordingSource:INotifyPropertyChanged
    {
        /// <summary>
        /// 是否被选中用于录制
        /// </summary>
        bool IsSelected { get; set; }
        
        /// <summary>
        /// 是否可被勾选
        /// </summary>
        bool IsCheckable { get; set; }

        /// <summary>
        /// 输出尺寸
        /// </summary>
        ScreenSize OutputSize { get; set; }
        
        /// <summary>
        /// 输出位置
        /// </summary>
        ScreenPoint Position { get; set; }
        
        /// <summary>
        /// 源区域矩形
        /// </summary>
        ScreenRect SourceRect { get; set; }
        
        /// <summary>
        /// 是否启用自定义位置
        /// </summary>
        bool IsCustomPositionEnabled { get; set; }
        
        /// <summary>
        /// 是否启用自定义输出尺寸
        /// </summary>
        bool IsCustomOutputSizeEnabled { get; set; }
        
        /// <summary>
        /// 是否启用自定义源区域
        /// </summary>
        bool IsCustomOutputSourceRectEnabled { get; set; }
        
        /// <summary>
        /// 是否启用视频捕获
        /// </summary>
        bool IsVideoCaptureEnabled { get; set; }

        /// <summary>
        /// 更新屏幕坐标，根据自定义设置应用位置和尺寸
        /// </summary>
        /// <param name="position">屏幕位置</param>
        /// <param name="size">屏幕尺寸</param>
        void UpdateScreenCoordinates(ScreenPoint position, ScreenSize size);
    }
}
