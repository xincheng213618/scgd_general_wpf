using ColorVision.UI;
using ColorVision.UI.Views;
using System.Threading.Tasks;

namespace ColorVision.Solution.Workspace
{
    /// <summary>
    /// 在所有其他初始化器之前创建 DockViewManager 并设置为 ViewManagerProvider.Current。
    /// 这确保设备控件在 ServiceInitializer (Order 5) 中调用 AddViewConfig 时，
    /// ViewManagerProvider.Current 已经返回 DockViewManager 而非 ViewGridManager 回退值。
    /// DockViewManager 的构造函数无需主窗口，AddView 也不访问 LayoutDocumentPane。
    /// </summary>
    public class DockViewManagerInitializer : InitializerBase
    {
        public override string Name => nameof(DockViewManagerInitializer);

        public override int Order => 0;

        public override Task InitializeAsync()
        {
            ViewManagerProvider.Current = new DockViewManager();
            return Task.CompletedTask;
        }
    }
}
