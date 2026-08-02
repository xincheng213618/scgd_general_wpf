using FlowEngineLib;
using FlowEngineLib.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow.Routing
{
    public static class FlowExecutionPolicyRuntimeAdapter
    {
        public static IReadOnlyList<FlowErrorRoute> ToRuntimeErrorRoutes(
            FlowExecutionPolicySnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            return snapshot.ErrorRoutes
                .Select(route => new FlowErrorRoute
                {
                    SourceNodeId = route.SourceNodeId,
                    TargetNodeId = route.TargetNodeId,
                    TargetInputIndex = route.TargetInputIndex,
                    FailureKinds = route.FailureKinds.ToArray()
                })
                .ToArray();
        }

        public static IReadOnlyList<FlowNodeRetryPolicy> ToRuntimeRetryPolicies(
            FlowExecutionPolicySnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            return snapshot.RetryPolicies
                .Select(policy => new FlowNodeRetryPolicy
                {
                    NodeId = policy.NodeId,
                    MaxAttempts = policy.MaxAttempts,
                    InitialDelayMs = policy.InitialDelayMs,
                    Backoff = policy.Backoff,
                    MaxDelayMs = policy.MaxDelayMs,
                    RetryableKinds = policy.RetryableKinds.ToArray()
                })
                .ToArray();
        }

        public static void Apply(
            FlowEngineControl control,
            FlowExecutionPolicySnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(control);
            ArgumentNullException.ThrowIfNull(snapshot);
            control.ConfigureFailureRoutes(
                ToRuntimeErrorRoutes(snapshot));
            control.ConfigureRetryPolicies(
                ToRuntimeRetryPolicies(snapshot));
        }

        public static void Clear(FlowEngineControl control)
        {
            ArgumentNullException.ThrowIfNull(control);
            control.ConfigureFailureRoutes(
                Array.Empty<FlowErrorRoute>());
            control.ConfigureRetryPolicies(
                Array.Empty<FlowNodeRetryPolicy>());
        }
    }

    internal static class FlowExecutionPolicyStoreProvider
    {
        private static readonly Lazy<IFlowExecutionPolicyStore> SharedStore =
            new(() => new JsonFlowExecutionPolicyStore(
                Path.Combine(
                    ColorVision.UI.Environments.DirAppData,
                    "Config",
                    "FlowExecutionPolicies")));

        public static IFlowExecutionPolicyStore Shared =>
            SharedStore.Value;
    }
}
