using ColorVision.UI;
using log4net;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.PhyCameras.Licenses
{
    /// <summary>
    /// 许可证检查初始化器 - 许可证状态由物理相机管理界面直接展示，不再弹出过期提醒窗口。
    /// </summary>
    public class LicenseCheckInitializer : MainWindowInitializedBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LicenseCheckInitializer));

        public LicenseCheckInitializer()
        {
            // Set order to run after database initialization but before other initializers
            Order = 100;
        }

        public override Task Initialize()
        {
            log.Info("相机许可证过期弹窗提醒已停用，状态由物理相机管理界面展示。");
            return Task.CompletedTask;
        }
    }
}
