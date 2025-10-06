using ColorVision.Engine.Services;
using ColorVision.ImageEditor;
using ColorVision.UI.Sorts;
using CVCommCore.CVAlgorithm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine
{
    /// <summary>
    /// 视图图像接口 - 定义图像查看器的基本功能
    /// </summary>
    public interface IViewImageA
    {
        ImageView ImageView { get; set; }
        ListView ListView { get; set; }
        ObservableCollection<GridViewColumnVisibility> LeftGridViewColumnVisibilitys { get; set; }
        TextBox SideTextBox { get; set; }
        
        void AddPOIPoint(List<POIPoint> PoiPoints);
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
        void Handle(IViewImageA view, ViewResultAlg result);

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
        /// 可以处理的算法类型列表
        /// </summary>
        public abstract List<ViewResultAlgType> CanHandle { get; }

        public virtual bool CanHandle1(ViewResultAlg result)
        {
            if (CanHandle.Contains(result.ResultType))
            {
                return true;
            }
            return false;
        }

        public abstract void Handle(IViewImageA view, ViewResultAlg result);

        public virtual void Load(IViewImageA view, ViewResultAlg result)
        {
        }

        public virtual void SideSave(ViewResultAlg result, string selectedPath)
        {
        }
    }
}
