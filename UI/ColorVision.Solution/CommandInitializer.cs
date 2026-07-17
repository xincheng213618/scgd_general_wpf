using ColorVision.UI;
using ColorVision.Solution.Explorer;
using ColorVision.Solution.Workspace;
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

            AddCommandBinding(
                mainWindow,
                SolutionWorkspaceCommands.OpenFolder,
                (_, _) => _ = SolutionManager.OpenFolderDialogAsync(),
                (_, e) => e.CanExecute = true);

            AddCommandBinding(
                mainWindow,
                SolutionWorkspaceCommands.CloseSolution,
                (_, _) => SolutionManager.GetInstance().TryCloseSolution(),
                (_, e) => e.CanExecute = SolutionManager.GetInstance().CanCloseSolution);

            AddCommandBinding(
                mainWindow,
                ApplicationCommands.Save,
                (_, _) => EditorDocumentService.TrySaveActiveDocument(),
                (_, e) => e.CanExecute = EditorDocumentService.CanSaveActiveDocument());

            AddCommandBinding(
                mainWindow,
                ApplicationCommands.Undo,
                (_, _) => ExecuteSolutionHistory(undo: true),
                (_, e) => e.CanExecute = SolutionManager.GetInstance().CurrentSolutionExplorer?.CanUndoSolutionOperation == true);

            AddCommandBinding(
                mainWindow,
                ApplicationCommands.Redo,
                (_, _) => ExecuteSolutionHistory(undo: false),
                (_, e) => e.CanExecute = SolutionManager.GetInstance().CurrentSolutionExplorer?.CanRedoSolutionOperation == true);

            AddCommandBinding(
                mainWindow,
                SolutionDocumentCommands.Reload,
                (_, _) => EditorDocumentService.TryReloadActiveDocument(),
                (_, e) => e.CanExecute = EditorDocumentService.CanReloadActiveDocument());

            AddCommandBinding(
                mainWindow,
                SolutionProjectCommands.BuildSolution,
                (_, _) => SolutionManager.GetInstance().CurrentSolutionExplorer?.BuildSolution(),
                (_, e) => e.CanExecute = SolutionManager.GetInstance().CurrentSolutionExplorer?.CanBuildSolution() == true);

            AddCommandBinding(
                mainWindow,
                SolutionProjectCommands.Run,
                (_, _) => SolutionManager.GetInstance().CurrentSolutionExplorer?.ExecuteStartupProject(ProjectCapabilityIds.Run),
                (_, e) => e.CanExecute = SolutionManager.GetInstance().CurrentSolutionExplorer?.CanExecuteStartupProject(ProjectCapabilityIds.Run) == true);

            AddCommandBinding(
                mainWindow,
                SolutionProjectCommands.Debug,
                (_, _) => SolutionManager.GetInstance().CurrentSolutionExplorer?.ExecuteStartupProject(ProjectCapabilityIds.Debug),
                (_, e) => e.CanExecute = SolutionManager.GetInstance().CurrentSolutionExplorer?.CanExecuteStartupProject(ProjectCapabilityIds.Debug) == true);

            AddCommandBinding(
                mainWindow,
                SolutionProjectCommands.ConfigurationManager,
                (_, _) => SolutionManager.GetInstance().CurrentSolutionExplorer?.ShowConfigurationManager(),
                (_, e) => e.CanExecute = SolutionManager.GetInstance().CurrentSolutionExplorer != null);
        }

        private static void ExecuteSolutionHistory(bool undo)
        {
            SolutionExplorer? explorer = SolutionManager.GetInstance().CurrentSolutionExplorer;
            if (explorer == null)
                return;

            bool succeeded = undo
                ? explorer.TryUndoSolutionOperation(out string errorMessage)
                : explorer.TryRedoSolutionOperation(out errorMessage);
            if (succeeded || string.IsNullOrWhiteSpace(errorMessage))
                return;

            MessageBox.Show(
                Application.Current?.GetActiveWindow(),
                errorMessage,
                undo ? "撤销解决方案操作失败" : "重做解决方案操作失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private static void AddCommandBinding(Window window, ICommand command, ExecutedRoutedEventHandler executed, CanExecuteRoutedEventHandler canExecute)
        {
            if (window.CommandBindings.OfType<CommandBinding>().Any(binding => binding.Command == command))
                return;

            window.CommandBindings.Add(new CommandBinding(command, executed, canExecute));
        }
    }

}
