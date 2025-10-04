#pragma warning disable CS1591
using System;
using System.Threading.Tasks;
using System.Windows.Media;
using ColorVision.Core;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// 图像编辑宿主上下文（瘦接口）。
    /// 非 UI 控件接口，仅暴露能力与状态，便于解耦与测试。
    /// </summary>
    public interface IImageEditorContext
    {
        // 基本状态
        ImageViewConfig Config { get; }

        /// <summary>
        /// 当前显示图（通常对应 ViewBitmapSource）。只读访问。
        /// </summary>
        ImageSource? Source { get; }

        /// <summary>
        /// 当前处理图（临时效果预览）。只读访问。
        /// </summary>
        ImageSource? FunctionImage { get; }

        /// <summary>
        /// 当前图像的底层 HImage 缓存（若可用）。
        /// </summary>
        HImage? HImage { get; }

        // 操作能力
        /// <summary>
        /// 设置新的源图像并刷新 UI（不会自动清空处理图）。
        /// </summary>
        void SetImageSource(ImageSource imageSource);

        /// <summary>
        /// 应用处理后的图像为预览图（FunctionImage），并切换显示。
        /// </summary>
        void ApplyProcessedImage(ImageSource imageSource);

        /// <summary>
        /// 向画布添加可视对象（叠加层）。
        /// </summary>
        void AddVisual(Visual visual);

        /// <summary>
        /// 从画布移除可视对象（叠加层）。
        /// </summary>
        void RemoveVisual(Visual visual);

        /// <summary>
        /// 根据当前图像、缩放等状态更新标尺/缩放显示。
        /// </summary>
        void UpdateZoomAndScale();

        // 线程/调度能力
        /// <summary>
        /// 在 UI 线程执行。
        /// </summary>
        void RunOnUI(Action action);

        /// <summary>
        /// 在后台线程运行任务。
        /// </summary>
        void RunBackground(Func<Task> action);

        // 事件
        /// <summary>
        /// 当 Source 或 FunctionImage 发生切换/变更时触发。
        /// </summary>
        event EventHandler? ImageChanged;

        /// <summary>
        /// 叠加层添加事件。
        /// </summary>
        event EventHandler<Visual>? VisualAdded;

        /// <summary>
        /// 叠加层移除事件。
        /// </summary>
        event EventHandler<Visual>? VisualRemoved;
    }
}
