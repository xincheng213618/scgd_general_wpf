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
        private Action? _activateInteractiveTerminal;
        private Action? _activateRunTerminal;
        private Func<TerminalControl?>? _getSelectedTerminal;
        private string? _pendingScriptPath;
        public const string PanelId = "TerminalPanel";

        private TerminalService() { }

        internal void SetTerminalControls(
            TerminalControl interactiveTerminalControl,
            TerminalControl runTerminalControl,
            Action activateInteractiveTerminal,
            Action activateRunTerminal,
            Func<TerminalControl?> getSelectedTerminal)
        {
            _interactiveTerminalControl = interactiveTerminalControl;
            _runTerminalControl = runTerminalControl;
            _activateInteractiveTerminal = activateInteractiveTerminal;
            _activateRunTerminal = activateRunTerminal;
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
                _activateInteractiveTerminal = null;
                _activateRunTerminal = null;
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
        /// Send a command string to the terminal's shell.
        /// </summary>
        public void SendCommand(string command)
        {
            if (!ShowPanel())
                return;

            var terminalControl = GetInteractiveTerminalControl();
            if (terminalControl == null) return;
            _activateInteractiveTerminal?.Invoke();
            terminalControl.NotifyPanelActivated();
            terminalControl.SendCommand(command);
        }

        public void SendCommand(string command, string workingDirectory)
        {
            TrySendCommand(command, workingDirectory);
        }

        public bool TrySendCommand(string command, string workingDirectory)
        {
            if (!ShowPanel())
                return false;

            var terminalControl = GetInteractiveTerminalControl();
            if (terminalControl == null) return false;
            _activateInteractiveTerminal?.Invoke();
            terminalControl.NotifyPanelActivated();
            terminalControl.SendCommand(command, workingDirectory);
            return true;
        }

        public bool TrySendCommandBatch(IReadOnlyList<TerminalCommandRequest> commands)
        {
            if (commands.Count == 0)
                return false;

            if (!ShowPanel())
                return false;

            var terminalControl = GetInteractiveTerminalControl();
            if (terminalControl == null)
                return false;
            _activateInteractiveTerminal?.Invoke();
            terminalControl.NotifyPanelActivated();
            terminalControl.SendCommandBatch(commands);
            return true;
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
                        Header = "终端",
                        Content = interactiveTerminalControl,
                    };
                    var runTab = new TabItem
                    {
                        Header = "运行",
                        Content = runTerminalControl,
                    };
                    var tabControl = new TabControl();
                    tabControl.Items.Add(interactiveTab);
                    tabControl.Items.Add(runTab);
                    tabControl.SelectedItem = interactiveTab;

                    if (Application.Current != null)
                    {
                        Application.Current.Exit += (_, _) =>
                        {
                            interactiveTerminalControl.Dispose();
                            runTerminalControl.Dispose();
                        };
                    }

                    TerminalService.GetInstance().SetTerminalControls(
                        interactiveTerminalControl,
                        runTerminalControl,
                        () => tabControl.SelectedItem = interactiveTab,
                        () => tabControl.SelectedItem = runTab,
                        () => ReferenceEquals(tabControl.SelectedItem, runTab)
                            ? runTerminalControl
                            : interactiveTerminalControl);
                    return tabControl;
                },
                "终端",
                PanelPosition.Bottom,
                isDefaultVisible: false);
        }
    }
}
