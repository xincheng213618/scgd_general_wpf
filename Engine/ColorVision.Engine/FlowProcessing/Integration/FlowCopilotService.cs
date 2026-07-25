using ColorVision.UI;
using log4net;
using System;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.Integration;

public sealed class FlowCopilotService : ICopilotBusinessContextSource
{
    private static readonly ILog log = LogManager.GetLogger(typeof(FlowCopilotService));
    private IDisposable? _agentExtensionRegistration;

    internal FlowEngineManager Manager { get; }
    public FlowCopilotContextService Context { get; }
    public FlowCopilotGraphEditor GraphEditor { get; }

    public FlowCopilotService(FlowEngineManager manager)
    {
        Manager = manager ?? throw new ArgumentNullException(nameof(manager));
        Context = new FlowCopilotContextService(manager);
        GraphEditor = new FlowCopilotGraphEditor(manager, Context);
        EnsureAgentExtensionRegistered();
    }

    public void AskAboutCurrentFlow()
    {
        CopilotFlowContextSnapshot snapshot = Context.CaptureSnapshot();
        CopilotContextItem contextItem = CopilotBusinessContextBuilder.BuildFlowContextItem(snapshot);
        CopilotBusinessContextBundle bundle = CopilotBusinessContextBundle.FromItem(snapshot.SourceId, contextItem);
        string prompt = CopilotBusinessContextCoordinator.BuildFlowDiagnosisPrompt(snapshot);
        var result = CopilotBusinessContextCoordinator.DispatchDiagnosis(bundle, prompt);

        if (!result.WasSent)
        {
            MessageBox.Show(
                Application.Current.GetActiveWindow(),
                result.StatusMessage,
                "ColorVision",
                MessageBoxButton.OK,
                result.IsAvailable ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
    }

    public CopilotBusinessContextBundle CaptureCopilotContext()
    {
        CopilotFlowContextSnapshot snapshot = Context.CaptureSnapshot();
        return CopilotBusinessContextBundle.FromItem(
            snapshot.SourceId,
            CopilotBusinessContextBuilder.BuildFlowContextItem(snapshot));
    }

    public void PublishContext()
    {
        try
        {
            CopilotFlowContextSnapshot snapshot = Context.CaptureSnapshot();
            if (!CopilotFlowContextProvider.HasMeaningfulSnapshot(snapshot))
                return;

            CopilotBusinessContextCoordinator.Publish(CopilotBusinessContextBundle.FromItem(
                snapshot.SourceId,
                CopilotBusinessContextBuilder.BuildFlowContextItem(snapshot)));
        }
        catch (Exception ex)
        {
            log.Debug($"Could not publish the active Flow context to Copilot: {ex.Message}");
        }
    }

    private void EnsureAgentExtensionRegistered()
    {
        if (_agentExtensionRegistration != null)
            return;

        try
        {
            _agentExtensionRegistration = CopilotFlowAgentExtension.Register(
                CopilotAgentExtensionRegistry.Shared,
                CopilotFlowContextProvider.Create(this),
                GetType().Assembly.GetName().Version?.ToString());
        }
        catch (Exception ex)
        {
            log.Warn("Could not register the Flow Engine Copilot Agent extension.", ex);
        }
    }
}
