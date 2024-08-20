using ColorVision.UI;
using System.IO;
using System.Windows;
using ColorVision.UI.Shell;

namespace ColorVision.Solution
{
    public class SolutionManagerInitializer : IInitializer
    {
        private readonly IMessageUpdater log;

        public SolutionManagerInitializer(IMessageUpdater messageUpdater)
        {
            log = messageUpdater;
        }

        public int Order => 1;

        public async Task InitializeAsync()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var solutionManager = SolutionManager.GetInstance();

                // 解析命令行参数
                bool su = false;
                var parser = ArgumentParser.GetInstance();
                parser.AddArgument("solutionpath", false, "s");
                parser.Parse();
                var solutionpath = parser.GetValue("solutionpath");

                if (solutionpath != null)
                {
                    su = solutionManager.OpenSolution(solutionpath);
                }
                // 检查 cvtest 目录
                string directoryPath = "D:\\CVTest";
                string fileExtension = "*.cvsln";

                if (!su && Directory.Exists(directoryPath))
                {
                    log.UpdateMessage("检测到存在服务模式目录");
                    var files = Directory.GetFiles(directoryPath, fileExtension);
                    if (files.Length == 0)
                    {
                        log.UpdateMessage("检测到服务默认路径下，不存在工程，正在创建默认项目");
                        Application.Current.Dispatcher.Invoke(() => solutionManager.CreateSolution(directoryPath));
                        su = true;
                    }
                    else
                    {
                        su = false;
                    }
                }


                // 检查默认解决方案目录
                if (!su)
                {
                    if (solutionManager.SolutionHistory.RecentFiles.Count > 0)
                    {
                        su = solutionManager.OpenSolution(solutionManager.SolutionHistory.RecentFiles[0]);
                    }

                    JumpListManager jumpListManager = new JumpListManager();
                    jumpListManager.AddRecentFiles(solutionManager.SolutionHistory.RecentFiles);

                    if (!su)
                    {
                        string Default = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ColorVision";
                        if (!Directory.Exists(Default))
                            Directory.CreateDirectory(Default);

                        string DefaultSolution = Default + "\\" + "Default";
                        if (!Directory.Exists(DefaultSolution))
                            Directory.CreateDirectory(DefaultSolution);
                        solutionManager.CreateSolution(DefaultSolution);
                    }
                }
            });
        }
    }
}
