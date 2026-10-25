using LocalizedText = global::ColorVision.DisplayText;
using ColorVision.Engine.Media;
using ColorVision.ImageEditor.BatchProcessing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotOpenBatchImageProcessingTool : ICopilotAgentDrivenTool
    {
        private readonly BatchImageProcessor _processor;
        private readonly Func<IReadOnlyList<BatchImageItem>, CancellationToken, Task<int>> _openWindowAsync;
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new Dictionary<string, object?>
            {
                ["sources"] = new { type = "array", minItems = 1, maxItems = 32, description = "Optional authorized image files or directories to append to the native window for review, without converting. Absolute or workspace-relative paths; at most 500 images after discovery.", items = new { type = "string", minLength = 1, maxLength = 4096 } },
                ["recursive"] = new { type = "boolean", description = "Search subdirectories of sources. Defaults to false; does not change existing window settings." },
            },
            additionalProperties = false,
        }));

        public CopilotOpenBatchImageProcessingTool()
            : this(new BatchImageProcessor([new StandardBatchImageLoader(), new CVRawBatchImageLoader()]), OpenWindowAsync)
        {
        }

        internal CopilotOpenBatchImageProcessingTool(BatchImageProcessor processor, Func<IReadOnlyList<BatchImageItem>, CancellationToken, Task<int>> openWindowAsync)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _openWindowAsync = openWindowAsync ?? throw new ArgumentNullException(nameof(openWindowAsync));
        }

        public string Name => "OpenBatchImageProcessing";

        public string Description => "Open ColorVision's native batch image processor for CVRAW, CVCIE, TIFF, PNG, JPEG, BMP, and WebP inputs. Optionally prefill authorized sources; directory recursion defaults to false. Appends to an idle existing window without changing its output or algorithm settings. Never starts conversion: the user reviews files and settings before starting.";

        public CopilotToolAccess Access => CopilotToolAccess.Write;

        public CopilotToolRiskLevel RiskLevel => CopilotToolRiskLevel.Low;

        public CopilotToolApprovalMode ApprovalMode => CopilotToolApprovalMode.Never;

        public CopilotToolIdempotency Idempotency => CopilotToolIdempotency.Idempotent;

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && Application.Current != null
                && CopilotToolIntentPolicy.NeedsBatchImageProcessing(request)
                && !CopilotToolIntentPolicy.NeedsBatchImageConversionExecution(request);
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            if (!Schema.TryBind(toolInput?.Arguments ?? new Dictionary<string, object?>(), out var bound, out var error))
                return Failure("The batch image inputs are invalid.", error);
            var sources = bound.Arguments.TryGetValue("sources", out var sourceValue)
                ? JsonSerializer.SerializeToElement(sourceValue).EnumerateArray().Select(item => item.GetString()!).ToArray()
                : Array.Empty<string>();
            var recursive = bound.Arguments.TryGetValue("recursive", out var recursiveValue)
                && JsonSerializer.SerializeToElement(recursiveValue).GetBoolean();
            var items = Array.Empty<BatchImageItem>();
            if (sources.Length > 0)
            {
                var resolved = await Task.Run(() =>
                {
                    var success = CopilotBatchImageSourceResolver.TryResolveItems(request, sources, recursive, _processor, cancellationToken, out var prepared, out var message);
                    return (Success: success, Items: prepared, Error: message);
                }, cancellationToken);
                if (!resolved.Success)
                    return Failure("The batch image inputs could not be prepared.", resolved.Error);
                items = resolved.Items;
            }

            cancellationToken.ThrowIfCancellationRequested();
            int added;
            try
            {
                added = await _openWindowAsync(items, cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException)
            {
                return Failure("The batch image window could not accept these inputs.", ex.Message);
            }
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Opened the native batch image processor.",
                Content = items.Length == 0
                    ? LocalizedText.Get("Choose input files or a folder, select '仅转换格式' for format-only conversion, review the output settings, and start the batch when ready.")
                    : $"Prepared {items.Length} image file(s); appended {added} new item(s) to the batch window. No images were converted. Review the list, output format, destination and algorithm, then start the batch when ready.",
            };
        }

        private CopilotToolResult Failure(string summary, string error) => new()
        {
            ToolName = Name,
            Success = false,
            Summary = summary,
            ErrorMessage = error,
            FailureKind = CopilotToolFailureKind.Validation,
        };

        private static Task<int> OpenWindowAsync(IReadOnlyList<BatchImageItem> items, CancellationToken cancellationToken)
        {
            if (Application.Current == null)
                throw new InvalidOperationException("Application.Current is null.");
            return CopilotUiDispatcher.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var existing = Application.Current.Windows.OfType<BatchImageProcessingWindow>().FirstOrDefault();
                if (existing != null)
                {
                    var added = items.Count == 0 ? 0 : existing.AddPreparedInputs(items);
                    if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                    existing.Activate();
                    return added;
                }

                var window = new BatchImageProcessingWindow { Owner = Application.Current.GetActiveWindow() };
                try
                {
                    var added = window.AddPreparedInputs(items);
                    window.Show();
                    window.Activate();
                    return added;
                }
                catch
                {
                    window.Close();
                    throw;
                }
            }, cancellationToken);
        }
    }
}
