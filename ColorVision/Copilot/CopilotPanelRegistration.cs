#pragma warning disable CA1822
using LocalizedText = global::ColorVision.DisplayText;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Copilot
{
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

                var panel = new CopilotChatPanel();
                InitializePanel(panel, GetOrCreateViewModel);
                _panel = panel;
                return _panel;
            }, fallback: null) ?? throw new InvalidOperationException("The Copilot UI is shutting down.");
        }

        internal static void InitializePanel(CopilotChatPanel panel, Func<CopilotChatViewModel> viewModelFactory)
        {
            var originalContent = panel.Content;
            StackPanel? failureContent = null;

            void TryInitialize()
            {
                CopilotChatViewModel viewModel;
                try
                {
                    viewModel = viewModelFactory();
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    System.Diagnostics.Trace.TraceError(
                        $"Copilot conversation state could not be read: {CopilotUserFacingErrorFormatter.Sanitize(exception.Message)}");
                    if (failureContent == null)
                    {
                        failureContent = new StackPanel { Margin = new Thickness(24) };
                        var explanation = new TextBlock
                        {
                            Text = LocalizedText.Get("暂时无法读取 Copilot 会话记录。记录未被覆盖，请确认文件未被占用且具有读取权限，然后重试。"),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 16),
                        };
                        explanation.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                        var retry = new Button
                        {
                            Content = LocalizedText.Get("重试"),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            MinWidth = 80,
                            Padding = new Thickness(16, 6, 16, 6),
                        };
                        retry.Click += (_, _) => TryInitialize();
                        failureContent.Children.Add(explanation);
                        failureContent.Children.Add(retry);
                    }
                    panel.Content = failureContent;
                    return;
                }

                panel.DataContext = viewModel;
                panel.Content = originalContent;
            }

            TryInitialize();
        }

        public void ShowPanel()
        {
            CopilotUiDispatcher.Invoke(() => WorkspaceManager.LayoutManager?.ShowPanel(PanelId));
        }

        public bool ShowConversation(string? conversationId)
        {
            return CopilotUiDispatcher.Invoke(() =>
            {
                var viewModel = GetOrCreateViewModel();
                WorkspaceManager.LayoutManager?.ShowPanel(PanelId);
                return viewModel.TrySelectConversation(conversationId);
            }, fallback: false);
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

        private static CopilotAgentMode MapPromptMode(CopilotPromptMode mode)
        {
            return mode switch
            {
                CopilotPromptMode.Chat => CopilotAgentMode.Chat,
                CopilotPromptMode.Explain => CopilotAgentMode.Explain,
                CopilotPromptMode.Web => CopilotAgentMode.Web,
                CopilotPromptMode.Code => CopilotAgentMode.Code,
                CopilotPromptMode.Diagnose => CopilotAgentMode.Diagnose,
                CopilotPromptMode.Plan => CopilotAgentMode.Plan,
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

            // Materialize the service while the main workspace is being composed so
            // context-menu callers can resolve it before the panel is opened once.
            var panelService = CopilotPanelService.GetInstance();
            layoutManager.RegisterPanel(
                CopilotPanelService.PanelId,
                panelService.GetOrCreatePanel,
                CopilotUiText.CopilotPanelTitle,
                PanelPosition.Right);
        }
    }

}
