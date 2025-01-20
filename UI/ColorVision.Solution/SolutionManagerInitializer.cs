using ColorVision.UI;
using System.IO;
using ColorVision.UI.Shell;

namespace ColorVision.Solution
{
    public class SolutionManagerInitializer : InitializerBase
    {
        private readonly IMessageUpdater log;

        public SolutionManagerInitializer(IMessageUpdater messageUpdater)
        {
            log = messageUpdater;
        }

        public override string Name => nameof(SolutionManagerInitializer);

        public override int Order => 1;

        public override async Task InitializeAsync()
        {
            // 解析命令行参数
            bool su = false;
            var parser = ArgumentParser.GetInstance();
            parser.AddArgument("solutionpath", false, "s");
            parser.Parse();
            var solutionpath = parser.GetValue("solutionpath");

            var solutionManager = SolutionManager.GetInstance();
            if (solutionpath != null)
            {
                su = solutionManager.OpenSolution(solutionpath);
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
        }
    }
}
