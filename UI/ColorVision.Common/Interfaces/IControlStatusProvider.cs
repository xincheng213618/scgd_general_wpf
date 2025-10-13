using System.Collections.Generic;

namespace ColorVision.UI
{
    /// <summary>
    /// 控件状态信息提供者接口
    /// 实现此接口的控件可以在被选中时，在状态栏显示相关的状态信息
    /// </summary>
    public interface IControlStatusProvider
    {
        /// <summary>
        /// 获取当前控件的状态信息
        /// </summary>
        /// <returns>状态信息的键值对集合</returns>
        IEnumerable<KeyValuePair<string, string>> GetStatusInfo();
    }
}
