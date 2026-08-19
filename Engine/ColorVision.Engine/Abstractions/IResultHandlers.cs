#pragma warning disable CA1822
using ColorVision.Engine.Services;
using ColorVision.ImageEditor;
using ColorVision.UI.Sorts;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Controls;

namespace ColorVision.Engine
{
    /// <summary>
    /// 视图图像接口 - 定义图像查看器的基本功能
    /// </summary>
    public class ViewResultContext
    {
        public ImageView ImageView { get; set; }
        public ListView ListView { get; set; }
        public ObservableCollection<GridViewColumnVisibility> LeftGridViewColumnVisibilitys { get; set; }
        public TextBox SideTextBox { get; set; }
    }

    /// <summary>
    /// 结果处理接口 - 定义算法结果的处理方式
    /// </summary>
    public interface IResultHandle
    {
        /// <summary>
        /// 判断是否可以处理指定的结果
        /// </summary>
        bool CanHandle1(ViewResultAlg result);

        /// <summary>
        /// 处理算法结果
        /// </summary>
        void Handle(ViewResultContext context, ViewResultAlg result);

        /// <summary>
        /// 保存侧边栏数据
        /// </summary>
        void SideSave(ViewResultAlg result, string selectedPath);
    }

    /// <summary>
    /// 结果处理基类 - 提供 IResultHandle 的默认实现
    /// </summary>
    public abstract class IResultHandleBase : IResultHandle
    {
        /// <summary>
        /// 处理器显示名称
        /// </summary>
        public virtual string Name => GetType().Name;

        protected const double OverlayFontSize = 10;
        protected const double OverlayPenThickness = 1;

        protected static string FormatNumber(double? value) => value?.ToString("F3", CultureInfo.InvariantCulture) ?? string.Empty;

        protected static void OpenSourceImage(ViewResultContext context, ViewResultAlg result)
        {
            if (File.Exists(result.FilePath))
                context.ImageView.OpenImage(result.FilePath);
        }

        /// <summary>
        /// 可以处理的算法类型列表
        /// </summary>
        public abstract List<ViewResultAlgType> CanHandle { get; }

        public virtual bool CanHandle1(ViewResultAlg result) => CanHandle.Contains(result.ResultType);


        public abstract void Handle(ViewResultContext context, ViewResultAlg result);

        public virtual void Load(ViewResultContext ctx, ViewResultAlg result)
        {
        }

        public virtual void SideSave(ViewResultAlg result, string selectedPath)
        {
        }

    }
}
