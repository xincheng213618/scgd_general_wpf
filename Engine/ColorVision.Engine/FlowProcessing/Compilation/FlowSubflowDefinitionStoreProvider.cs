using System;
using System.IO;

namespace ColorVision.Engine.FlowProcessing.Compilation;

internal static class FlowSubflowDefinitionStoreProvider
{
    private static readonly Lazy<IFlowSubflowDefinitionStore> SharedStore =
        new(() => new JsonFlowSubflowDefinitionStore(
            Path.Combine(
                ColorVision.UI.Environments.DirAppData,
                "Config",
                "FlowDefinitions")));

    public static IFlowSubflowDefinitionStore Shared =>
        SharedStore.Value;
}
