using ColorVision.Solution.Workspace;
using ColorVision.UI;

namespace ColorVision.Solution.Terminal
{
    public sealed record TerminalCommandRequest(string DisplayName, string Command, string WorkingDirectory);

    /// <summary>
    /// Singleton service that manages the terminal panel.
    /// Provides API for other components to run scripts/commands in the terminal.
    /// </summary>
    public class TerminalService
    {
        private static TerminalService? _instance;
        public static TerminalService GetInstance() => _instance ??= new TerminalService();

        private TerminalControl? _interactiveTerminalControl;
        private TerminalControl? _runTerminalControl;
        private TerminalControl? _projectTerminalControl;
        private int _terminalNumber = 1;
        public const string PanelId = "TerminalPanel";
        internal const string RunPanelId = "TerminalPanel.Run";
        internal const string ProjectPanelId = "TerminalPanel.Project";

        private TerminalService() { }

        internal TerminalControl CreatePrimaryTerminal()
        {
            _interactiveTerminalControl = new TerminalControl();
            return _interactiveTerminalControl;
        }

        internal void NewTerminal()
        {
            if (WorkspaceManager.LayoutManager is not { } layoutManager)
                return;

            int number = ++_terminalNumber;
            var terminal = new TerminalControl();
            layoutManager.ShowTransientPanel($"{PanelId}.{number}", terminal,
                $"{Properties.Resources.Sol_Terminal_Title} {number}", PanelPosition.Bottom, terminal.Dispose);
        }

        internal void ClearTerminalControl(TerminalControl control)
        {
            if (ReferenceEquals(_interactiveTerminalControl, control))
                _interactiveTerminalControl = null;
            if (ReferenceEquals(_runTerminalControl, control))
                _runTerminalControl = null;
            if (ReferenceEquals(_projectTerminalControl, control))
                _projectTerminalControl = null;
        }

        /// <summary>
        /// Run a script file in an independent docked terminal session.
        /// </summary>
        public void RunScript(string filePath)
        {
            if (WorkspaceManager.LayoutManager is not { } layoutManager)
                return;

            var terminalControl = _runTerminalControl ??= new TerminalControl();
            layoutManager.ShowTransientPanel(RunPanelId, terminalControl, Properties.Resources.Sol_Terminal_Run,
                PanelPosition.Bottom, terminalControl.Dispose);
            terminalControl.RunScript(filePath);
        }

        /// <summary>
        /// Run a command in the project terminal.
        /// </summary>
        public void SendCommand(string command) => TrySendCommand(command, null);

        public void SendCommand(string command, string workingDirectory)
        {
            TrySendCommand(command, workingDirectory);
        }

        public bool TrySendCommand(string command, string? workingDirectory)
        {
            string shell = _interactiveTerminalControl?.CurrentShell ?? "powershell";
            string directory = string.IsNullOrWhiteSpace(workingDirectory) ? TerminalControl.GetDefaultWorkingDirectory() : workingDirectory;
            if (!TerminalProjectCommand.CanRun(shell, command, directory))
                return false;

            return OpenProjectTerminal()?.TryStartProjectCommand(shell,
                TerminalProjectCommand.CreateCommandLine(shell, command), directory) == true;
        }

        public bool TrySendCommandBatch(IReadOnlyList<TerminalCommandRequest> commands)
        {
            string shell = _interactiveTerminalControl?.CurrentShell ?? "powershell";
            if (commands.Count == 0 || commands.Any(command => !TerminalProjectCommand.CanRun(shell, command.Command, command.WorkingDirectory)))
                return false;

            return OpenProjectTerminal()?.TryStartProjectCommand(shell,
                TerminalProjectCommand.CreateBatchCommandLine(shell, commands), workingDirectory: null) == true;
        }

        public void NotifyPanelActivated() => _interactiveTerminalControl?.NotifyPanelActivated();

        private TerminalControl? OpenProjectTerminal()
        {
            if (WorkspaceManager.LayoutManager is not { } layoutManager)
                return null;

            var terminalControl = _projectTerminalControl ??= new TerminalControl(isProjectTerminal: true);
            layoutManager.ShowTransientPanel(ProjectPanelId, terminalControl, DisplayText.Get("Terminal_ProjectCommands"),
                PanelPosition.Bottom, terminalControl.Dispose);
            return terminalControl;
        }
    }

    /// <summary>
    /// Registers the Terminal panel in the DockingManager.
    /// Discovered automatically via assembly scanning.
    /// </summary>
    public class TerminalPanelProvider : IDockPanelProvider
    {
        public int Order => 50;

        public void RegisterPanels()
        {
            var layoutManager = WorkspaceManager.LayoutManager;
            if (layoutManager == null) return;

            layoutManager.RegisterPanel(
                TerminalService.PanelId,
                () => TerminalService.GetInstance().CreatePrimaryTerminal(),
                Properties.Resources.Sol_Terminal_Title,
                PanelPosition.Bottom,
                isDefaultVisible: false);
        }
    }
}
