#pragma warning disable MAAI001
using Microsoft.Agents.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed partial class CopilotAgentExecutionContract
    {
        public static CopilotAgentExecutionContract Create(
            CopilotAgentRequest request,
            IReadOnlyList<ICopilotTool> availableTools)
        {
            ArgumentNullException.ThrowIfNull(request);
            availableTools ??= Array.Empty<ICopilotTool>();

            var needsBatchImageConversion = CopilotToolIntentPolicy.NeedsBatchImageConversionExecution(request);
            var attachedFilePaths = request.Attachments
                .Where(item => item?.Type == CopilotAttachmentType.File && !string.IsNullOrWhiteSpace(item.Value))
                .Select(item => NormalizePath(item.Value))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Where(path => !needsBatchImageConversion || !CopilotToolIntentPolicy.IsBatchImageFilePath(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var attachedFileReadTools = attachedFilePaths.Length > 0
                ? availableTools
                    .Where(tool => string.Equals(tool.Name, "ReadAttachedFile", StringComparison.OrdinalIgnoreCase))
                    .Select(tool => tool.Name)
                    .ToArray()
                : Array.Empty<string>();
            var requiredLocalFilePaths = request.ReadableLocalFilePaths
                .Select(NormalizeExistingFilePath)
                .Where(path => !string.IsNullOrWhiteSpace(path) && !attachedFilePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                .Where(path => !needsBatchImageConversion || !CopilotToolIntentPolicy.IsBatchImageFilePath(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var localFileReadTools = requiredLocalFilePaths.Length > 0
                ? availableTools
                    .Where(tool => string.Equals(tool.Name, "ReadLocalFile", StringComparison.OrdinalIgnoreCase))
                    .Select(tool => tool.Name)
                    .ToArray()
                : Array.Empty<string>();
            var prerequisiteToolGroups = new List<IEnumerable<string>>();
            if (attachedFileReadTools.Length > 0)
                prerequisiteToolGroups.Add(attachedFileReadTools);
            if (localFileReadTools.Length > 0)
                prerequisiteToolGroups.Add(localFileReadTools);
            var explicitlyDisallowsPublicWebAccess = CopilotToolIntentPolicy.ExplicitlyDisallowsPublicWebAccess(request);

            var workspaceApplyTools = availableTools.Where(CopilotToolIntentPolicy.IsWorkspaceApplyTool).Select(tool => tool.Name);
            var workspaceValidationTools = availableTools.Where(CopilotToolIntentPolicy.IsWorkspaceValidationTool).Select(tool => tool.Name);
            var workspaceRollbackTools = availableTools.Where(CopilotToolIntentPolicy.IsWorkspaceRollbackTool).Select(tool => tool.Name);
            var needsBackgroundShellExecution =
                CopilotToolIntentPolicy.NeedsBackgroundShellExecution(request);
            var shellExecutionTools = availableTools
                .Where(needsBackgroundShellExecution
                    ? CopilotToolIntentPolicy.IsBackgroundShellExecutionTool
                    : CopilotToolIntentPolicy.IsShellExecutionTool)
                .Select(tool => tool.Name);
            var batchImageProcessingTools = availableTools.Where(CopilotToolIntentPolicy.IsBatchImageProcessingTool).Select(tool => tool.Name);
            var batchImageConversionTools = availableTools.Where(CopilotToolIntentPolicy.IsBatchImageConversionTool).Select(tool => tool.Name);
            var needsValidation = CopilotToolIntentPolicy.NeedsWorkspaceValidation(request);
            var needsShellExecution = CopilotToolIntentPolicy.NeedsShellExecution(request);
            if (CopilotToolIntentPolicy.NeedsWorkspaceRollback(request))
            {
                return Required(
                    CopilotAgentExecutionRequirement.WorkspaceRollback,
                    [workspaceRollbackTools],
                    prerequisiteToolGroups,
                    attachedFilePaths,
                    requiredLocalFilePaths);
            }
            if (CopilotToolIntentPolicy.NeedsWorkspaceCreate(request))
            {
                return Required(
                    (needsShellExecution, needsValidation) switch
                    {
                        (true, true) => CopilotAgentExecutionRequirement.WorkspaceCreateAndShellExecutionAndValidation,
                        (true, false) => CopilotAgentExecutionRequirement.WorkspaceCreateAndShellExecution,
                        (false, true) => CopilotAgentExecutionRequirement.WorkspaceCreateAndValidation,
                        _ => CopilotAgentExecutionRequirement.WorkspaceCreate,
                    },
                    (needsShellExecution, needsValidation) switch
                    {
                        (true, true) => [workspaceApplyTools, shellExecutionTools, workspaceValidationTools],
                        (true, false) => [workspaceApplyTools, shellExecutionTools],
                        (false, true) => [workspaceApplyTools, workspaceValidationTools],
                        _ => [workspaceApplyTools],
                    },
                    prerequisiteToolGroups,
                    attachedFilePaths,
                    requiredLocalFilePaths);
            }
            if (CopilotToolIntentPolicy.NeedsWorkspaceEdit(request))
            {
                return Required(
                    (needsShellExecution, needsValidation) switch
                    {
                        (true, true) => CopilotAgentExecutionRequirement.WorkspaceEditAndShellExecutionAndValidation,
                        (true, false) => CopilotAgentExecutionRequirement.WorkspaceEditAndShellExecution,
                        (false, true) => CopilotAgentExecutionRequirement.WorkspaceEditAndValidation,
                        _ => CopilotAgentExecutionRequirement.WorkspaceEdit,
                    },
                    (needsShellExecution, needsValidation) switch
                    {
                        (true, true) => [workspaceApplyTools, shellExecutionTools, workspaceValidationTools],
                        (true, false) => [workspaceApplyTools, shellExecutionTools],
                        (false, true) => [workspaceApplyTools, workspaceValidationTools],
                        _ => [workspaceApplyTools],
                    },
                    prerequisiteToolGroups,
                    attachedFilePaths,
                    requiredLocalFilePaths);
            }
            if (request.Mode == CopilotAgentMode.Review)
            {
                var gitWorkingTreeTools = availableTools
                    .Where(tool => string.Equals(tool.Name, "InspectGitWorkingTree", StringComparison.OrdinalIgnoreCase))
                    .Select(tool => tool.Name)
                    .ToArray();
                var gitDiffTools = availableTools
                    .Where(tool => string.Equals(tool.Name, "InspectGitDiff", StringComparison.OrdinalIgnoreCase))
                    .Select(tool => tool.Name)
                    .ToArray();
                if (gitWorkingTreeTools.Length > 0 || gitDiffTools.Length > 0)
                {
                    return Required(
                        needsValidation
                            ? CopilotAgentExecutionRequirement.GitReviewAndWorkspaceValidation
                            : CopilotAgentExecutionRequirement.GitReviewEvidence,
                        needsValidation
                            ? [gitWorkingTreeTools, gitDiffTools, workspaceValidationTools]
                            : [gitWorkingTreeTools, gitDiffTools],
                        prerequisiteToolGroups,
                        attachedFilePaths,
                        requiredLocalFilePaths);
                }
            }
            if (needsValidation)
            {
                return Required(
                    CopilotAgentExecutionRequirement.WorkspaceValidation,
                    [workspaceValidationTools],
                    prerequisiteToolGroups,
                    attachedFilePaths,
                    requiredLocalFilePaths);
            }
            if (needsShellExecution)
            {
                return Required(
                    CopilotAgentExecutionRequirement.ShellExecution,
                    [shellExecutionTools],
                    prerequisiteToolGroups,
                    attachedFilePaths,
                    requiredLocalFilePaths);
            }
            if (CopilotToolIntentPolicy.NeedsBatchImageConversionExecution(request))
            {
                return Required(
                    CopilotAgentExecutionRequirement.BatchImageConversion,
                    [batchImageConversionTools],
                    prerequisiteToolGroups,
                    attachedFilePaths,
                    requiredLocalFilePaths);
            }
            if (CopilotToolIntentPolicy.NeedsBatchImageProcessing(request))
            {
                return Required(
                    CopilotAgentExecutionRequirement.BatchImageProcessing,
                    [batchImageProcessingTools],
                    prerequisiteToolGroups,
                    attachedFilePaths,
                    requiredLocalFilePaths);
            }

            var urlFetchTools = availableTools.Where(CopilotToolIntentPolicy.IsUrlFetchTool).Select(tool => tool.Name);
            var webSearchTools = availableTools.Where(CopilotToolIntentPolicy.IsPublicWebSearchTool).Select(tool => tool.Name);
            if (!explicitlyDisallowsPublicWebAccess && CopilotWebPageToolSupport.ExtractHttpUrls(request.UserText).Count > 0)
            {
                return Required(
                    CopilotAgentExecutionRequirement.DirectUrlEvidence,
                    [urlFetchTools.Concat(webSearchTools)],
                    prerequisiteToolGroups,
                    attachedFilePaths,
                    requiredLocalFilePaths);
            }

            if (!explicitlyDisallowsPublicWebAccess && CopilotToolIntentPolicy.ExplicitlyRequiresPublicWebSearch(request))
            {
                return Required(
                    CopilotAgentExecutionRequirement.PublicWebSearch,
                    [webSearchTools],
                    prerequisiteToolGroups,
                    attachedFilePaths,
                    requiredLocalFilePaths);
            }

            var requiredSuccessfulTools = request.RequiredSuccessfulToolNames
                .Where(requiredName => availableTools.Any(tool =>
                    string.Equals(tool.Name, requiredName, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (requiredSuccessfulTools.Length > 0)
            {
                return Required(
                    CopilotAgentExecutionRequirement.SubagentEvidence,
                    [requiredSuccessfulTools],
                    prerequisiteToolGroups,
                    attachedFilePaths,
                    requiredLocalFilePaths);
            }

            return prerequisiteToolGroups.Count > 0
                ? Required(
                    localFileReadTools.Length > 0
                        ? CopilotAgentExecutionRequirement.LocalFileEvidence
                        : CopilotAgentExecutionRequirement.AttachedFileEvidence,
                    Array.Empty<IEnumerable<string>>(),
                    prerequisiteToolGroups,
                    attachedFilePaths,
                    requiredLocalFilePaths)
                : None();
        }

        private static CopilotAgentExecutionContract Required(
            CopilotAgentExecutionRequirement requirement,
            IEnumerable<IEnumerable<string>> requiredToolGroups,
            IReadOnlyList<IEnumerable<string>> prerequisiteToolGroups,
            IReadOnlyList<string> requiredAttachedFilePaths,
            IReadOnlyList<string> requiredLocalFilePaths)
        {
            var groups = prerequisiteToolGroups.Concat(requiredToolGroups);
            return new CopilotAgentExecutionContract(requirement, groups, requiredAttachedFilePaths, requiredLocalFilePaths);
        }

        private static CopilotAgentExecutionContract None() => new(
            CopilotAgentExecutionRequirement.None,
            Array.Empty<IEnumerable<string>>());
    }
}
