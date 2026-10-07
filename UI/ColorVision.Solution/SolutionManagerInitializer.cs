using ColorVision.Solution.Explorer;
using ColorVision.UI;
using ColorVision.UI.Shell;
using System.Windows;

namespace ColorVision.Solution
{
    public class SolutionManagerInitializer : InitializerBase, IInitializerDependencies
    {
        public SolutionManagerInitializer() { }

        public override string Name => nameof(SolutionManagerInitializer);

        public override int Order => 1;
        public System.Collections.Generic.IReadOnlyCollection<string> Dependencies => [];

        public override async Task InitializeAsync() 
        {
            await Task.Delay(0);

            SolutionNodeFactory.InitializeRegistries();

            var parser = ArgumentParser.GetInstance();

            var input = parser.GetValue("input");

            parser.AddArgument("solutionpath", false, "s");
            parser.Parse();
            if (SolutionManager.IsSupportedOpenPath(input))
            {
                parser.SetValue("solutionpath", input!);
            }

            var solutionpath = parser.GetValue("solutionpath");

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SolutionManager.GetInstance();
            });

        }
    }
}
