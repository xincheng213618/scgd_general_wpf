using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.UI;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotOpenBatchImageProcessingTool : ICopilotAgentDrivenTool
    {
        public string Name => "OpenBatchImageProcessing";

        public string Description => "Open ColorVision's native batch image processor for CVRAW, CVCIE, TIFF, PNG, JPEG, BMP, and WebP inputs. Use the identity algorithm for format-only conversion, including CVRAW or CVCIE to TIFF. The window lets the user review files, recursion, output format, destination, suffix, and overwrite protection before starting.";

        public CopilotToolAccess Access => CopilotToolAccess.Write;

        public CopilotToolRiskLevel RiskLevel => CopilotToolRiskLevel.Low;

        public CopilotToolApprovalMode ApprovalMode => CopilotToolApprovalMode.Never;

        public CopilotToolIdempotency Idempotency => CopilotToolIdempotency.Idempotent;

        public CopilotToolInputSchema InputSchema => CopilotToolInputSchema.Empty;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && Application.Current != null
                && CopilotToolIntentPolicy.NeedsBatchImageProcessing(request);
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            if (Application.Current == null)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "The batch image processor could not be opened.",
                    ErrorMessage = "Application.Current is null.",
                };
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var existing = Application.Current.Windows
                    .OfType<BatchImageProcessingWindow>()
                    .FirstOrDefault();
                if (existing != null)
                {
                    if (existing.WindowState == WindowState.Minimized)
                        existing.WindowState = WindowState.Normal;
                    existing.Activate();
                    return;
                }

                var window = new BatchImageProcessingWindow
                {
                    Owner = Application.Current.GetActiveWindow(),
                };
                window.Show();
                window.Activate();
            });

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Opened the native batch image processor.",
                Content = "Choose input files or a folder, select '仅转换格式' for format-only conversion, review the output settings, and start the batch when ready.",
            };
        }
    }
}
