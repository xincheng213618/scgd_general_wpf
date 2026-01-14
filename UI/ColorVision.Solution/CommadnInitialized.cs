using ColorVision.UI;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Solution
{
    public class CommadnInitialized : MainWindowInitializedBase
    {
        public override Task Initialize()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                Application.Current.MainWindow.CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (s, e) => SolutionManager.GetInstance().NewCreateWindow(), (s, e) => e.CanExecute = true));
                Application.Current.MainWindow.CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (s, e) => SolutionManager.OpenSolutionWindow(), (s, e) => e.CanExecute = true));
            });
            return Task.CompletedTask;
        }
    }

}