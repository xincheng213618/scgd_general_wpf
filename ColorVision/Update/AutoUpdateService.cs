using ColorVision.Themes.Controls;
using ColorVision.UI;
using log4net;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update
{
    public class AutoUpdateService : IMainWindowInitialized
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AutoUpdateService));

        public Task Initialize() => Check();

        public static async Task Check()
        {
            // 如果是调试模式，不进行更新检测
            if (Debugger.IsAttached) return;

            //不在提示用户更新日志
            //await Task.Run(CheckVersion);

            if (AutoUpdateConfig.Instance.IsAutoUpdate)
            {
                await Task.Run(CheckUpdate);
            }
        }

        public static async Task CheckUpdate()
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                AutoUpdater.DeleteAllCachedUpdateFiles();
                AutoUpdater autoUpdater = AutoUpdater.GetInstance();
                await autoUpdater.CheckAndUpdateV1(false,true);
            });
        }

        //public static async Task CheckVersion()
        //{
        //    await Task.Delay(100);
        //    if (Assembly.GetExecutingAssembly().GetName().Version > MainWindowConfig.Instance.LastOpenVersion)
        //    {
        //        Application.Current.Dispatcher.Invoke(() =>
        //        {
        //            try
        //            {
        //                string? currentVersion = Assembly.GetExecutingAssembly().GetName()?.Version?.ToString();
        //                string changelogPath = "CHANGELOG.md";

        //                // 读取CHANGELOG.md文件的所有内容
        //                string changelogContent = File.ReadAllText(changelogPath);

        //                // 使用正则表达式来匹配当前版本的日志条目
        //                string versionPattern = $"## \\[{currentVersion}\\].*?\\n(.*?)(?=\\n## |$)";
        //                Match match = Regex.Match(changelogContent, versionPattern, RegexOptions.Singleline);

        //                if (match.Success)
        //                {
        //                    // 如果找到匹配项，提取变更日志
        //                    string changeLogForCurrentVersion = match.Groups[1].Value.Trim();
        //                    // 显示变更日志
        //                    MessageBox1.Show(Application.Current.GetActiveWindow(), $"{changeLogForCurrentVersion.ReplaceLineEndings()}", $"{currentVersion} {Properties.Resources.ChangeLog}：");
        //                }
        //                else
        //                {
        //                    // 如果未找到匹配项，说明没有为当前版本列出变更日志
        //                    MessageBox1.Show(Application.Current.GetActiveWindow(), "1.修复了一些已知的BUG", $"{currentVersion} {Properties.Resources.ChangeLog}：");
        //                }

        //            }
        //            catch (Exception ex)
        //            {
        //                log.Error(ex.Message);
        //            }
        //        });
        //    }
        //    MainWindowConfig.Instance.LastOpenVersion = Assembly.GetExecutingAssembly().GetName().Version;
        //}


    }
}
