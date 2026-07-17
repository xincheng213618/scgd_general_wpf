using ColorVision.Copilot;
using System.Text.Json;

namespace ColorVision.UI.Tests
{
    [CollectionDefinition("Copilot live context", DisableParallelization = true)]
    public sealed class CopilotLiveContextCollectionDefinition
    {
    }

    [Collection("Copilot live context")]
    public sealed class CopilotDiagnosisSafetyTests
    {
        [Fact]
        public void FlowDiagnosisPromptKeepsTemplateOperationsOutOfDiagnosis()
        {
            var prompt = CopilotBusinessContextCoordinator.BuildFlowDiagnosisPrompt(new CopilotFlowContextSnapshot());

            Assert.Contains("diagnosis turn is read-only", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("separate explicit user request", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("suggest_template_patch", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("preview_template_patch", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("apply_template_patch", prompt, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DiagnoseModeRejectsWriteToolsAtTheRegistryBoundary()
        {
            var request = new CopilotAgentRequest { Mode = CopilotAgentMode.Diagnose };

            Assert.True(CopilotToolRegistry.IsAllowedForMode(new CopilotTemplatePatchTool(), request));
            Assert.False(CopilotToolRegistry.IsAllowedForMode(new CopilotApplyTemplatePatchTool(), request));
        }

        [Fact]
        public void TemplatePatchToolsAreUnavailableDuringDiagnosis()
        {
            const string sourceId = "template-json-editor:diagnosis-test";
            CopilotLiveContextRegistry.Publish(new CopilotLiveContext { SourceId = sourceId });

            try
            {
                var request = new CopilotAgentRequest
                {
                    Mode = CopilotAgentMode.Diagnose,
                    UserText = "Adjust exposure, preview the change, and apply it.",
                };

                Assert.False(new CopilotTemplatePatchTool().CanHandle(request));
                Assert.False(new CopilotApplyTemplatePatchTool().CanHandle(request));
            }
            finally
            {
                CopilotLiveContextRegistry.Clear(sourceId);
            }
        }

        [Fact]
        public async Task TemplatePatchExecutionRejectsDiagnosisWithoutInvokingTheApplication()
        {
            var invoker = new RecordingCapabilityInvoker();
            var request = new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Diagnose,
                UserText = "Apply the template change.",
            };

            var previewResult = await new CopilotTemplatePatchTool(invoker).ExecuteAsync(
                request,
                new CopilotAgentToolInput { Query = "{\"proposed_changes\":{\"Exposure\":12}}" },
                CancellationToken.None);
            var applyResult = await new CopilotApplyTemplatePatchTool(invoker).ExecuteAsync(
                request,
                new CopilotAgentToolInput { Query = "{\"preview_id\":\"preview-1\"}" },
                CancellationToken.None);

            Assert.False(previewResult.Success);
            Assert.False(applyResult.Success);
            Assert.Contains("Diagnose mode", previewResult.Summary, StringComparison.Ordinal);
            Assert.Contains("Diagnose mode", applyResult.Summary, StringComparison.Ordinal);
            Assert.Equal(0, invoker.InvocationCount);
        }

        [Fact]
        public void TemplatePatchToolsRemainAvailableForExplicitAutoRequests()
        {
            const string sourceId = "template-json-editor:auto-test";
            CopilotLiveContextRegistry.Publish(new CopilotLiveContext { SourceId = sourceId });

            try
            {
                var previewRequest = new CopilotAgentRequest
                {
                    Mode = CopilotAgentMode.Auto,
                    UserText = "Adjust the exposure parameter.",
                };
                var applyRequest = new CopilotAgentRequest
                {
                    Mode = CopilotAgentMode.Auto,
                    UserText = "Apply this preview.",
                };

                Assert.True(new CopilotTemplatePatchTool().CanHandle(previewRequest));
                Assert.False(new CopilotTemplatePatchTool().CanHandle(applyRequest));
                Assert.True(new CopilotApplyTemplatePatchTool().CanHandle(applyRequest));
            }
            finally
            {
                CopilotLiveContextRegistry.Clear(sourceId);
            }
        }

        private sealed class RecordingCapabilityInvoker : ICopilotApplicationCapabilityInvoker
        {
            public int InvocationCount { get; private set; }

            public Task<CopilotApplicationCapabilityCallResult> InvokeAsync(
                string capabilityName,
                IReadOnlyDictionary<string, JsonElement>? arguments,
                CopilotApplicationCapabilityCaller caller,
                CancellationToken cancellationToken)
            {
                InvocationCount++;
                return Task.FromResult(new CopilotApplicationCapabilityCallResult { Success = true });
            }
        }
    }
}
