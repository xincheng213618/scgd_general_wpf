#pragma warning disable CA1822
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
        private static readonly Lazy<CopilotPanelService> Instance = new(() => new CopilotPanelService());

        private CopilotChatPanel? _panel;
        private CopilotChatViewModel? _viewModel;

        public const string PanelId = "CopilotChatPanel";

        private CopilotPanelService()
        {
            CopilotServiceRegistry.Register(this);
        }

        public static CopilotPanelService GetInstance() => Instance.Value;

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

        public CopilotChatViewModel GetOrCreateViewModel()
        {
            return CopilotUiDispatcher.Invoke(
                () => _viewModel ??= new CopilotChatViewModel(),
                fallback: null) ?? throw new InvalidOperationException("The Copilot UI is shutting down.");
        }

        public CopilotChatPanel GetOrCreatePanel()
        {
            return CopilotUiDispatcher.Invoke(() =>
            {
                if (_panel != null)
                    return _panel;

                _panel = new CopilotChatPanel
                {
                    DataContext = GetOrCreateViewModel(),
                };
                return _panel;
            }, fallback: null) ?? throw new InvalidOperationException("The Copilot UI is shutting down.");
        }

        public void ShowPanel()
        {
            CopilotUiDispatcher.Invoke(() => WorkspaceManager.LayoutManager?.ShowPanel(PanelId));
        }

        public bool Ask(CopilotPromptRequest request)
        {
            return CopilotUiDispatcher.Invoke(() => AskOnUiThread(request), fallback: false);
        }

        private bool AskOnUiThread(CopilotPromptRequest request)
        {
            if (request == null || !CanShowPanel)
                return false;

            var contextItems = request.ContextItems ?? Array.Empty<CopilotContextItem>();
            var attachContextSnapshot = request.AttachContextSnapshot && contextItems.Count > 0;
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
                attachContextSnapshot ? contextItems : null);
            return queueResult.Accepted;
        }

        public CopilotPromptDispatchResult DispatchExceptionPrompt(string prompt)
        {
            return CopilotUiDispatcher.Invoke(
                () => DispatchExceptionPromptOnUiThread(prompt),
                new CopilotPromptDispatchResult(false, false, "The main AI panel is shutting down."));
        }

        private CopilotPromptDispatchResult DispatchExceptionPromptOnUiThread(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return new CopilotPromptDispatchResult(false, false, "No exception content is available to send.");

            if (!CanShowPanel)
                return new CopilotPromptDispatchResult(false, false, "The main AI panel is not ready yet.");

            if (!IsConfigured)
                return new CopilotPromptDispatchResult(false, false, "AI is not configured, so the exception diagnosis request cannot be sent.");

            var viewModel = GetOrCreateViewModel();
            ShowPanel();

            var queueResult = viewModel.QueueExternalPrompt(prompt, startNewConversation: true, sendNow: true, mode: CopilotAgentMode.Diagnose);
            if (queueResult.WasSent)
                return new CopilotPromptDispatchResult(true, true, "Sent to the main AI panel.");

            return new CopilotPromptDispatchResult(queueResult.Accepted, false, viewModel.IsBusy
                ? "AI is busy. The exception context was placed in the input box."
                : "The exception context was placed in the AI input box.");
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

            builder.AppendLine("# Caller-provided context");
            foreach (var item in contextItems)
            {
                builder.Append("## ").AppendLine(string.IsNullOrWhiteSpace(item.Title) ? "Context" : item.Title.Trim());
                if (!string.IsNullOrWhiteSpace(item.Summary))
                    builder.Append("Summary: ").AppendLine(item.Summary.Trim());

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
                CopilotUiText.CopilotPanelTitle,
                PanelPosition.Right);
        }
    }

}
