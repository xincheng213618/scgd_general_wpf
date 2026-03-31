using ColorVision.Solution.Workspace;
using ColorVision.UI;
using System.Windows.Controls;

namespace ColorVision.Solution.Terminal
{
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

        /// <summary>
        /// Run a script file in the terminal panel and activate it.
        /// </summary>
        public void RunScript(string filePath)
        {
            if (_terminalControl == null) return;
            ActivatePanel();
            _terminalControl.RunScript(filePath);
        }

        /// <summary>
        /// Run a command in the terminal panel and activate it.
        /// </summary>
        public void RunCommand(string fileName, string arguments, string? workingDirectory = null)
        {
            if (_terminalControl == null) return;
            ActivatePanel();
            _terminalControl.RunProcess(fileName, arguments, workingDirectory);
        }

        private void ActivatePanel()
        {
            // Show and activate the terminal panel in the DockingManager
            var layoutManager = WorkspaceManager.LayoutManager;
            if (layoutManager != null)
            {
                layoutManager.ShowPanel(PanelId);
            }
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

            TerminalService.GetInstance().SetTerminalControl(terminalControl);

            layoutManager.RegisterPanel(TerminalService.PanelId, grid, "终端", PanelPosition.Bottom);
        }
    }
}
