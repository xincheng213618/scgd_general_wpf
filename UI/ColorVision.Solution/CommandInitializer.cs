using ColorVision.UI;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Solution
{
    public class CommandInitializer : MainWindowInitializedBase
    {
        public override Task Initialize()
        {
            var application = Application.Current;
            if (application == null)
                return Task.CompletedTask;

            if (application.Dispatcher.CheckAccess())
            {
                RegisterCommandBindings();
                return Task.CompletedTask;
            }

            return application.Dispatcher.InvokeAsync(RegisterCommandBindings).Task;
        }

        private static void RegisterCommandBindings()
        {
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow == null)
                return;

            AddCommandBinding(
                mainWindow,
                ApplicationCommands.New,
                (s, e) => SolutionManager.GetInstance().NewCreateWindow(),
                (s, e) => e.CanExecute = true);

            AddCommandBinding(
                mainWindow,
                ApplicationCommands.Open,
                (s, e) => SolutionManager.OpenSolutionWindow(),
                (s, e) => e.CanExecute = true);
        }

        private static void AddCommandBinding(Window window, ICommand command, ExecutedRoutedEventHandler executed, CanExecuteRoutedEventHandler canExecute)
        {
            if (window.CommandBindings.OfType<CommandBinding>().Any(binding => binding.Command == command))
                return;

            window.CommandBindings.Add(new CommandBinding(command, executed, canExecute));
        }
    }

}
