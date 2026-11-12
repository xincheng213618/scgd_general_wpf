using ColorVision.Engine.FlowProcessing;
using ColorVision.UI;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp;

internal sealed partial class CopilotMcpToolEnvironment
{
    private static async Task<CopilotFlowValidationSnapshot?> CreateDefaultFlowGraphValidationAsync(string expectedRevision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manager = FlowEngineManager.Current;
        if (manager?.View == null) return null;
        return await CopilotUiDispatcher.InvokeAsync(manager.View.Dispatcher, () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(FlowEngineManager.Current, manager)) throw new CopilotFlowValidationScopeChangedException();
            if (manager.View?.STNodeEditorMain == null) return null;
            var result = manager.Copilot.ValidateCopilotFlowGraph(expectedRevision);
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(FlowEngineManager.Current, manager)) throw new CopilotFlowValidationScopeChangedException();
            return result;
        }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class CopilotFlowValidationScopeChangedException : InvalidOperationException
{
    public CopilotFlowValidationScopeChangedException() : base("The active Flow editor changed before validation completed.") { }
}
