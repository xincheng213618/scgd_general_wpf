using ColorVision.UI;
using log4net;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update
{
    public class AutoUpdateService : MainWindowInitializedBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AutoUpdateService));

        public override Task Initialize() => Check();

        public static Task Check()
        {
            // 如果是调试模式，不进行更新检测
            if (Debugger.IsAttached) return Task.CompletedTask;

            if (AutoUpdateConfig.Instance.IsAutoUpdate)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AutoUpdater.GetInstance().CheckAndUpdateV1(false, true);
                });
            }
            return Task.CompletedTask;
        }

    }
}
