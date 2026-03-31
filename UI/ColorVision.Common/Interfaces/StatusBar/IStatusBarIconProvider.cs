using System.Collections.Generic;

namespace ColorVision.UI
{
    /// <summary>
    /// 上下文感知的状态栏提供者，当活动文档变化时动态提供状态栏项。
    /// 例如：当 ImageEditor 被激活时，提供图像基本信息的状态栏项。
    /// </summary>
    public interface IStatusBarIconProvider
    {
        /// <summary>
        /// 获取当前上下文的状态栏元数据
        /// </summary>
        IEnumerable<StatusBarMeta> GetStatusBarIconMetadata();

        /// <summary>
        /// 当前提供者是否匹配指定的内容对象（例如 LayoutDocument.Content）
        /// </summary>
        bool IsMatch(object content);
    }
}
