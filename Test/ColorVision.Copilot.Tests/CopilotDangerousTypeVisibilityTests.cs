using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.Reflection;

namespace ColorVision.Copilot.Tests
{
    public sealed class CopilotDangerousTypeVisibilityTests
    {
        public static IEnumerable<object[]> DangerousImplementationTypes()
        {
            yield return [typeof(CopilotMcpRequestHandler)];
            yield return [typeof(CopilotMcpHttpRequest)];
            yield return [typeof(CopilotMcpHttpResponse)];
            yield return [typeof(CopilotMcpToolDispatcher)];
            yield return [typeof(CopilotMcpToolEnvironment)];
            yield return [typeof(CopilotMcpRuntimeSettings)];
            yield return [typeof(CopilotMcpServer)];
            yield return [typeof(CopilotMcpTemplatePatchPreview)];
            yield return [typeof(CopilotMcpTemplatePatchPreviewStore)];
            yield return [typeof(CopilotMcpAuditEntry)];
            yield return [typeof(CopilotMcpAuditLogger)];
            yield return [typeof(CopilotMcpDiagnosticSnapshot)];
            yield return [typeof(CopilotMcpDiagnostics)];
            yield return [typeof(CopilotTemplatePatchApplyRequest)];
            yield return [typeof(CopilotFlowPatchRequest)];
            yield return [typeof(CopilotLocalGitDiffResult)];
            yield return [typeof(CopilotLocalGitDiffService)];
            yield return [typeof(CopilotToolExecutionAuditEntry)];
            yield return [typeof(CopilotToolExecutionAuditLogger)];
            yield return [typeof(CopilotAgentTaskEventJournalContext)];
            yield return [typeof(CopilotAgentTaskEventJournalRegistry)];
            yield return [typeof(CopilotMcpConfirmationStore)];
            yield return [typeof(CopilotConfirmationRequestContext)];
            yield return [typeof(CopilotConfirmationReviewContext)];
            yield return [typeof(CopilotApprovalSourceKind)];
            yield return [typeof(CopilotShellCommandService)];
            yield return [typeof(CopilotShellProcessCommand)];
            yield return [typeof(CopilotShellProcessResult)];
            yield return [typeof(ICopilotShellProcessRunner)];
            yield return [typeof(CopilotShellProcessRunner)];
            yield return [typeof(CopilotCodexShellEnvironmentPolicy)];
            yield return [typeof(CopilotCodexShellEnvironmentPolicyLayer)];
            yield return [typeof(CopilotTemporaryRedactedOutputArchive)];
            yield return [typeof(CopilotShellCommandOutputCapture)];
            yield return [typeof(CopilotShellCommandOutputArchiveRegistry)];
            yield return [typeof(CopilotToolOutputArchiveRegistry)];
            yield return [typeof(CopilotGitWorkingTreeInspectionService)];
            yield return [typeof(CopilotGitDiffInspectionService)];
            yield return [typeof(CopilotDatabaseSqlService)];
            yield return [typeof(CopilotMySqlDatabaseSqlExecutor)];
            yield return [typeof(CopilotWorkspacePatchStore)];
            yield return [typeof(CopilotWorkspaceValidationCommand)];
            yield return [typeof(CopilotWorkspaceValidationProcessResult)];
            yield return [typeof(ICopilotWorkspaceValidationRunner)];
            yield return [typeof(CopilotWorkspaceValidationService)];
            yield return [typeof(CopilotWorkspaceValidationProcessRunner)];
            yield return [typeof(CopilotApplicationCapability)];
        }

        [Theory]
        [MemberData(nameof(DangerousImplementationTypes))]
        public void DangerousConcreteImplementationIsNotPublic(Type implementationType)
        {
            Assert.False(implementationType.IsVisible, $"{implementationType.FullName} must remain assembly-internal.");
            Assert.True(implementationType.IsNotPublic, $"{implementationType.FullName} must not be a public top-level type.");
        }

        [Fact]
        public void PublicFacadeConstructorsDoNotExposeDangerousImplementationTypes()
        {
            var dangerousTypes = DangerousImplementationTypes()
                .Select(data => (Type)data[0])
                .ToHashSet();
            Type[] facadeTypes =
            [
                typeof(CopilotTcpPortInspectionService),
                typeof(CopilotShellCommandTool),
                typeof(CopilotReadShellCommandOutputTool),
                typeof(CopilotReadToolOutputTool),
                typeof(CopilotInspectGitWorkingTreeTool),
                typeof(CopilotInspectGitDiffTool),
                typeof(CopilotQueryDatabaseSqlTool),
                typeof(CopilotExecuteDatabaseSqlTool),
                typeof(CopilotPreviewWorkspacePatchEnvelopeTool),
                typeof(CopilotApplyWorkspacePatchEnvelopeTool),
                typeof(CopilotRollbackWorkspacePatchEnvelopeTool),
                typeof(CopilotWorkspaceValidationTool),
            ];

            foreach (var facadeType in facadeTypes)
            {
                var exposedParameterTypes = facadeType
                    .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .SelectMany(constructor => constructor.GetParameters())
                    .Select(parameter => parameter.ParameterType)
                    .ToArray();

                Assert.DoesNotContain(exposedParameterTypes, dangerousTypes.Contains);
            }
        }

        [Fact]
        public void PublicConfirmableActionDoesNotExposeApprovalRequestScope()
        {
            var publicRequestContext = typeof(ConfirmableAction).GetProperty(
                "RequestContext",
                BindingFlags.Public | BindingFlags.Instance);

            Assert.Null(publicRequestContext);
        }
    }
}
