using System.Threading.Tasks;

namespace ColorVision.UI
{
    /// <summary>
    /// 主窗口的加载事件
    /// </summary>
    public interface IMainWindowInitialized
    {
        Task Initialize();
    }
}
