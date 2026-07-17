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

        private TerminalControl? _terminalControl;
        public const string PanelId = "TerminalPanel";

        private TerminalService() { }

        internal void SetTerminalControl(TerminalControl control)
        {
            _terminalControl = control;
        }

        internal void ClearTerminalControl(TerminalControl control)
        {
            if (ReferenceEquals(_terminalControl, control))
                _terminalControl = null;
        }

        /// <summary>
        /// Run a script file in the terminal panel and activate it.
        /// </summary>
        public void RunScript(string filePath)
        {
            var terminalControl = GetActiveTerminalControl();
            if (terminalControl == null) return;
            ActivatePanel();
            terminalControl.RunScript(filePath);
        }

        /// <summary>
        /// Send a command string to the terminal's shell.
        /// </summary>
        public void SendCommand(string command)
        {
            var terminalControl = GetActiveTerminalControl();
            if (terminalControl == null) return;
            ActivatePanel();
            terminalControl.SendCommand(command);
        }

        public void SendCommand(string command, string workingDirectory)
        {
            TrySendCommand(command, workingDirectory);
        }

        public bool TrySendCommand(string command, string workingDirectory)
        {
            var terminalControl = GetActiveTerminalControl();
            if (terminalControl == null) return false;
            ActivatePanel();
            terminalControl.SendCommand(command, workingDirectory);
            return true;
        }

        public bool TrySendCommandBatch(IReadOnlyList<TerminalCommandRequest> commands)
        {
            if (commands.Count == 0)
                return false;

            var terminalControl = GetActiveTerminalControl();
            if (terminalControl == null)
                return false;
            ActivatePanel();
            terminalControl.SendCommandBatch(commands);
            return true;
        }

        public void NotifyPanelActivated()
        {
            var terminalControl = GetActiveTerminalControl();
            terminalControl?.NotifyPanelActivated();
        }

        private TerminalControl? GetActiveTerminalControl()
        {
            if (_terminalControl?.IsDisposed == true)
                _terminalControl = null;

            return _terminalControl;
        }

        private void ActivatePanel()
        {
            var layoutManager = WorkspaceManager.LayoutManager;
            layoutManager?.ShowPanel(PanelId);
            NotifyPanelActivated();
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

            var grid = new Grid();
            var terminalControl = new TerminalControl();
            grid.Children.Add(terminalControl);

            if (Application.Current != null)
                Application.Current.Exit += (_, _) => terminalControl.Dispose();

            TerminalService.GetInstance().SetTerminalControl(terminalControl);

            layoutManager.RegisterPanel(TerminalService.PanelId, grid, "终端", PanelPosition.Bottom, isDefaultVisible: false);
        }
    }
}
