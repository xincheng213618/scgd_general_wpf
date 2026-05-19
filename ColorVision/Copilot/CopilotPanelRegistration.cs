using ColorVision.Solution.Workspace;
using ColorVision.UI;
using System;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    public readonly record struct CopilotPromptDispatchResult(bool IsAvailable, bool WasSent, string StatusMessage);
    public readonly record struct CopilotPromptQueueResult(bool Accepted, bool WasSent);

    public sealed class CopilotPanelService : ICopilotService
    {
        private static CopilotPanelService? _instance;

        private CopilotChatPanel? _panel;
        private CopilotChatViewModel? _viewModel;

        public const string PanelId = "CopilotChatPanel";

        private CopilotPanelService()
        {
            CopilotServiceRegistry.Register(this);
        }

        public static CopilotPanelService GetInstance() => _instance ??= new CopilotPanelService();

        public bool CanShowPanel => WorkspaceManager.LayoutManager != null;

        public bool IsAvailable => CanShowPanel;

        public bool IsConfigured
        {
            get
            {
                try
                {
                    return CopilotConfig.Instance.IsConfigured;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool CanAskFromException => CanShowPanel && IsConfigured;

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

        public bool Ask(CopilotPromptRequest request)
        {
            if (request == null || !CanShowPanel)
                return false;

            var attachContextSnapshot = request.AttachContextSnapshot && request.ContextItems.Count > 0;
            var prompt = attachContextSnapshot
                ? (request.Prompt ?? string.Empty).Trim()
                : BuildPromptContent(request);

            if (string.IsNullOrWhiteSpace(prompt))
                return false;

            var viewModel = GetOrCreateViewModel();
            ShowPanel();
            var queueResult = viewModel.QueueExternalPrompt(
                prompt,
                request.StartNewConversation,
                request.SendNow,
                MapPromptMode(request.Mode),
                attachContextSnapshot ? request.ContextAttachmentTitle : null,
                attachContextSnapshot ? request.ContextAttachmentSourceId : null,
                attachContextSnapshot ? request.ContextItems : null);
            return queueResult.Accepted;
        }

        public CopilotPromptDispatchResult DispatchExceptionPrompt(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return new CopilotPromptDispatchResult(false, false, "没有可发送的异常内容。");

            if (!CanShowPanel)
                return new CopilotPromptDispatchResult(false, false, "主界面的 AI 视图尚未就绪。");

            if (!IsConfigured)
                return new CopilotPromptDispatchResult(false, false, "当前 AI 未配置，无法直接发送异常诊断请求。");

            var viewModel = GetOrCreateViewModel();
            ShowPanel();

            var queueResult = viewModel.QueueExternalPrompt(prompt, startNewConversation: true, sendNow: true, mode: CopilotAgentMode.Diagnose);
            if (queueResult.WasSent)
                return new CopilotPromptDispatchResult(true, true, "已发送到主界面的 AI 视图。");

            return new CopilotPromptDispatchResult(queueResult.Accepted, false, viewModel.IsBusy
                ? "AI 正在生成，已把异常上下文放到输入框。"
                : "已把异常上下文放到 AI 输入框。");
        }

        private static CopilotAgentMode MapPromptMode(CopilotPromptMode mode)
        {
            return mode switch
            {
                CopilotPromptMode.Chat => CopilotAgentMode.Chat,
                CopilotPromptMode.Explain => CopilotAgentMode.Explain,
                CopilotPromptMode.Web => CopilotAgentMode.Web,
                CopilotPromptMode.Code => CopilotAgentMode.Code,
                CopilotPromptMode.Diagnose => CopilotAgentMode.Diagnose,
                _ => CopilotAgentMode.Auto,
            };
        }

        private static string BuildPromptContent(CopilotPromptRequest request)
        {
            var builder = new StringBuilder();
            builder.Append((request.Prompt ?? string.Empty).Trim());

            var contextItems = request.ContextItems?
                .Where(item => item != null)
                .Where(item => !string.IsNullOrWhiteSpace(item.Title)
                    || !string.IsNullOrWhiteSpace(item.Summary)
                    || !string.IsNullOrWhiteSpace(item.Content))
                .ToArray()
                ?? Array.Empty<CopilotContextItem>();

            if (contextItems.Length == 0)
                return builder.ToString().Trim();

            if (builder.Length > 0)
                builder.AppendLine().AppendLine();

            builder.AppendLine("# 调用方附加上下文");
            foreach (var item in contextItems)
            {
                builder.Append("## ").AppendLine(string.IsNullOrWhiteSpace(item.Title) ? "上下文" : item.Title.Trim());
                if (!string.IsNullOrWhiteSpace(item.Summary))
                    builder.Append("摘要：").AppendLine(item.Summary.Trim());

                if (!string.IsNullOrWhiteSpace(item.Content))
                    builder.AppendLine(item.Content.Trim());

                builder.AppendLine();
            }

            return builder.ToString().Trim();
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