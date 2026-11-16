using ColorVision.Solution.Workspace;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;

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
        private Action? _activateRunTerminal;
        private Func<TerminalControl>? _openProjectTerminal;
        private Func<TerminalControl?>? _getSelectedTerminal;
        private string? _pendingScriptPath;
        public const string PanelId = "TerminalPanel";

        private TerminalService() { }

        internal void SetTerminalControls(
            TerminalControl interactiveTerminalControl,
            TerminalControl runTerminalControl,
            Action activateRunTerminal,
            Func<TerminalControl> openProjectTerminal,
            Func<TerminalControl?> getSelectedTerminal)
        {
            _interactiveTerminalControl = interactiveTerminalControl;
            _runTerminalControl = runTerminalControl;
            _activateRunTerminal = activateRunTerminal;
            _openProjectTerminal = openProjectTerminal;
            _getSelectedTerminal = getSelectedTerminal;

            if (_pendingScriptPath is string pendingScriptPath)
            {
                _pendingScriptPath = null;
                _activateRunTerminal();
                runTerminalControl.RunScript(pendingScriptPath);
            }
        }

        internal void ClearTerminalControl(TerminalControl control)
        {
            if (ReferenceEquals(_interactiveTerminalControl, control))
                _interactiveTerminalControl = null;
            if (ReferenceEquals(_runTerminalControl, control))
                _runTerminalControl = null;

            if (_interactiveTerminalControl == null && _runTerminalControl == null)
            {
                _activateRunTerminal = null;
                _openProjectTerminal = null;
                _getSelectedTerminal = null;
            }
        }

        /// <summary>
        /// Run a script file in the terminal panel and activate it.
        /// </summary>
        public void RunScript(string filePath)
        {
            if (!ShowPanel())
                return;

            var terminalControl = GetRunTerminalControl();
            if (terminalControl == null)
            {
                // Some dock hosts create their content on the next layout pass. Only the latest
                // request is relevant; retaining a queue would start and immediately kill each
                // earlier script when the run terminal is finally materialized.
                _pendingScriptPath = filePath;
                return;
            }

            _pendingScriptPath = null;
            _activateRunTerminal?.Invoke();
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
            string shell = GetInteractiveTerminalControl()?.CurrentShell ?? "powershell";
            string directory = string.IsNullOrWhiteSpace(workingDirectory) ? TerminalControl.GetDefaultWorkingDirectory() : workingDirectory;
            if (!TerminalProjectCommand.CanRun(shell, command, directory))
                return false;

            if (!ShowPanel())
                return false;

            return _openProjectTerminal?.Invoke().TryStartProjectCommand(shell,
                TerminalProjectCommand.CreateCommandLine(shell, command), directory) == true;
        }

        public bool TrySendCommandBatch(IReadOnlyList<TerminalCommandRequest> commands)
        {
            string shell = GetInteractiveTerminalControl()?.CurrentShell ?? "powershell";
            if (commands.Count == 0 || commands.Any(command => !TerminalProjectCommand.CanRun(shell, command.Command, command.WorkingDirectory)))
                return false;

            if (!ShowPanel())
                return false;

            return _openProjectTerminal?.Invoke().TryStartProjectCommand(shell,
                TerminalProjectCommand.CreateBatchCommandLine(shell, commands), workingDirectory: null) == true;
        }

        public void NotifyPanelActivated()
        {
            var terminalControl = _getSelectedTerminal?.Invoke() ?? GetInteractiveTerminalControl();
            terminalControl?.NotifyPanelActivated();
        }

        private TerminalControl? GetInteractiveTerminalControl()
        {
            if (_interactiveTerminalControl?.IsDisposed == true)
                _interactiveTerminalControl = null;

            return _interactiveTerminalControl;
        }

        private TerminalControl? GetRunTerminalControl()
        {
            if (_runTerminalControl?.IsDisposed == true)
                _runTerminalControl = null;

            return _runTerminalControl;
        }

        private bool ShowPanel()
        {
            var layoutManager = WorkspaceManager.LayoutManager;
            if (layoutManager == null)
                return false;

            layoutManager.ShowPanel(PanelId);
            return true;
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
                () =>
                {
                    var interactiveTerminalControl = new TerminalControl();
                    var runTerminalControl = new TerminalControl();
                    var interactiveTab = new TabItem
                    {
                        Header = Properties.Resources.Sol_Terminal_Title,
                        Content = interactiveTerminalControl,
                    };
                    var runTab = new TabItem
                    {
                        Header = Properties.Resources.Sol_Terminal_Run,
                        Content = runTerminalControl,
                    };
                    var tabControl = new TabControl();
                    tabControl.Items.Add(interactiveTab);
                    tabControl.Items.Add(runTab);
                    tabControl.SelectedItem = interactiveTab;
                    TabItem? projectTab = null;

                    TerminalControl OpenProjectTerminal()
                    {
                        if (projectTab?.Content is not TerminalControl projectTerminal || projectTerminal.IsDisposed)
                        {
                            projectTerminal = new TerminalControl(isProjectTerminal: true);
                            if (projectTab == null)
                            {
                                projectTab = new TabItem { Header = DisplayText.Get("Terminal_ProjectCommands") };
                                tabControl.Items.Add(projectTab);
                            }
                            projectTab.Content = projectTerminal;
                        }
                        tabControl.SelectedItem = projectTab;
                        return projectTerminal;
                    }

                    if (Application.Current != null)
                    {
                        Application.Current.Exit += (_, _) =>
                        {
                            interactiveTerminalControl.Dispose();
                            runTerminalControl.Dispose();
                            (projectTab?.Content as TerminalControl)?.Dispose();
                        };
                    }

                    TerminalService.GetInstance().SetTerminalControls(
                        interactiveTerminalControl,
                        runTerminalControl,
                        () => tabControl.SelectedItem = runTab,
                        OpenProjectTerminal,
                        () => (tabControl.SelectedItem as TabItem)?.Content as TerminalControl);
                    return tabControl;
                },
                Properties.Resources.Sol_Terminal_Title,
                PanelPosition.Bottom,
                isDefaultVisible: false);
        }
    }
}
