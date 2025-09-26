using ColorVision.UI;
using ColorVision.UI.Shell;
using System.IO;
using System.Windows;

namespace ColorVision.Solution
{
    public class SolutionManagerInitializer : InitializerBase
    {
        public SolutionManagerInitializer() { }

        public override string Name => nameof(SolutionManagerInitializer);

        public override int Order => 1;

        public override async Task InitializeAsync() 
        {
            await Task.Delay(0);
            var parser = ArgumentParser.GetInstance();

            var input = parser.GetValue("input");

            parser.AddArgument("solutionpath", false, "s");
            parser.Parse();
            if (File.Exists(input) && Path.GetExtension(input) == ".cvsln")
            {
                parser.SetValue("solutionpath",input);
            }

            var solutionpath = parser.GetValue("solutionpath");

            _= Application.Current.Dispatcher.BeginInvoke(() =>
            {
                SolutionManager.GetInstance();
            });

        }
    }
}
