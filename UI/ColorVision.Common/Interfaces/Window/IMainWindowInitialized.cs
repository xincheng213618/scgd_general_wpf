using System.Collections.Generic;
using System.Threading.Tasks;

namespace ColorVision.UI
{
    /// <summary>
    /// 主窗口的加载事件
    /// </summary>
    public interface IMainWindowInitialized
    {
        public string Name { get; }
        public int Order { get; }
        Task Initialize();
    }

    public abstract class MainWindowInitializedBase : IMainWindowInitialized
    {
        public virtual string Name => GetType().Name;

        public virtual int Order { get; set; } = 1;
        public abstract Task Initialize();
    }

}
