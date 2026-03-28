using System.Collections.Generic;
using System.Windows.Controls;

namespace ColorVision.UI.Views
{
    /// <summary>
    /// 视图管理器抽象接口 — 定义管理多视图布局的核心操作。
    /// 
    /// 当前实现：ViewGridManager（基于 WPF Grid 的 N 宫格布局）。
    /// 未来可替换为基于 AvalonDock LayoutDocument 的实现，使每个 IView 成为独立的可停靠文档。
    /// 
    /// 所有 IView 实现和调用方应通过此接口交互，而非直接引用具体实现，
    /// 以便在不修改 IView 实现的前提下切换视图管理策略。
    /// </summary>
    public interface IViewManager
    {
        /// <summary>
        /// 当前最大视图数变化事件
        /// </summary>
        event ViewMaxChangedHandler ViewMaxChangedEvent;

        /// <summary>
        /// 当前最大视图数（即显示窗格的数量）
        /// </summary>
        int ViewMax { get; set; }

        /// <summary>
        /// 当前已注册的所有视图控件
        /// </summary>
        List<Control> Views { get; }

        /// <summary>
        /// 当前主视图（第一个窗格中的控件）
        /// </summary>
        Control? CurrentView { get; }

        /// <summary>
        /// 添加视图控件，追加到视图列表末尾
        /// </summary>
        int AddView(Control control);

        /// <summary>
        /// 添加视图控件，插入到视图列表指定位置
        /// </summary>
        int AddView(int index, Control control);

        /// <summary>
        /// 按索引移除视图
        /// </summary>
        void RemoveView(int index);

        /// <summary>
        /// 按控件引用移除视图
        /// </summary>
        void RemoveView(Control control);

        /// <summary>
        /// 设置视图控件的显示位置索引
        /// <para>viewIndex >= 0: 放入指定窗格</para>
        /// <para>viewIndex == -1: 隐藏</para>
        /// <para>viewIndex == -2: 独立窗口</para>
        /// </summary>
        void SetViewIndex(Control control, int viewIndex);

        /// <summary>
        /// 检查指定索引的窗格是否为空
        /// </summary>
        bool IsGridEmpty(int index);

        /// <summary>
        /// 获取当前窗格数量
        /// </summary>
        int GetViewNums();

        /// <summary>
        /// 设置视图布局为指定数量的窗格（标准布局）
        /// </summary>
        void SetViewGrid(int nums);

        /// <summary>
        /// 切换为单视图模式，显示指定索引的视图
        /// </summary>
        void SetOneView(int main);

        /// <summary>
        /// 切换为单视图模式，显示指定控件
        /// </summary>
        void SetOneView(Control control);

        /// <summary>
        /// 按数量自动分配视图
        /// <para>num == -1: 自动分配所有已注册视图</para>
        /// </summary>
        void SetViewNum(int num);

        /// <summary>
        /// 将视图控件弹出为独立窗口
        /// </summary>
        void SetSingleWindowView(Control control);
    }
}
