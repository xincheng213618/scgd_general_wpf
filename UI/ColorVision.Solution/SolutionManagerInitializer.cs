using ColorVision.UI;
using ColorVision.UI.Shell;
using System.Windows;

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
            await Task.Delay(0);
            var parser = ArgumentParser.GetInstance();
            parser.AddArgument("solutionpath", false, "s");
            parser.Parse();
            var solutionpath = parser.GetValue("solutionpath");
            _= Application.Current.Dispatcher.BeginInvoke(() =>
            {
                SolutionManager.GetInstance();
            });

        }
    }
}
