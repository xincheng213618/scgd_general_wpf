using System;
using System.Collections.Generic;

namespace ColorVision.UI
{
    /// <summary>
    /// 活动文档状态栏提供者。
    /// 当一个视图/编辑器成为活动文档时，它提供的状态栏项会显示；
    /// 当它失去焦点时，这些项会被移除。
    /// 例如：ImageView 可以在活动时显示图片尺寸、格式等信息。
    /// </summary>
    public interface IActiveDocumentStatusProvider
    {
        /// <summary>
        /// 获取当前活动文档的状态栏项
        /// </summary>
        IEnumerable<StatusBarMeta> GetActiveStatusBarItems();

        /// <summary>
        /// 当状态栏项数据发生变化时触发（如异步加载完成后）。
        /// StatusBarManager 会订阅此事件，在触发时重新获取并刷新状态栏项。
        /// </summary>
        event EventHandler StatusBarItemsChanged;
    }
}
