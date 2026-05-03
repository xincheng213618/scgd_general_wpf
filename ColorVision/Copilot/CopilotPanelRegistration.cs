using ColorVision.Solution.Workspace;
using ColorVision.UI;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotPanelService
    {
        private static CopilotPanelService? _instance;

        private CopilotChatPanel? _panel;
        private CopilotChatViewModel? _viewModel;

        public const string PanelId = "CopilotChatPanel";

        public static CopilotPanelService GetInstance() => _instance ??= new CopilotPanelService();

        public CopilotChatViewModel GetOrCreateViewModel() => _viewModel ??= new CopilotChatViewModel();

        public CopilotChatPanel GetOrCreatePanel()
        {
            if (_panel != null)
                return _panel;

            _panel = new CopilotChatPanel
            {
                DataContext = GetOrCreateViewModel(),
            };
            return _panel;
        }

        public void ShowPanel()
        {
            WorkspaceManager.LayoutManager?.ShowPanel(PanelId);
        }
    }

    public sealed class CopilotDockPanelProvider : IDockPanelProvider
    {
        public int Order => 210;

        public void RegisterPanels()
        {
            var layoutManager = WorkspaceManager.LayoutManager;
            if (layoutManager == null)
                return;

            layoutManager.RegisterPanel(
                CopilotPanelService.PanelId,
                CopilotPanelService.GetInstance().GetOrCreatePanel(),
                "对话助手",
                PanelPosition.Right);
        }
    }

}