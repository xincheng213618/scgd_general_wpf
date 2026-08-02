using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Flow.Routing;
using System;

namespace ColorVision.Engine.Templates.Flow.Versioning
{
    internal sealed record FlowVersionRestoreRequest(
        FlowParam FlowParam,
        FlowRevision Revision,
        string? ExpectedContentHash);

    internal sealed record FlowVersionRestoreResult(
        bool Succeeded,
        string? LoadedContentHash,
        bool VersionCatalogUpdated,
        string? FailureMessage,
        string? RollbackFailure)
    {
        public static FlowVersionRestoreResult Success(
            string? loadedContentHash,
            bool versionCatalogUpdated)
        {
            return new FlowVersionRestoreResult(
                Succeeded: true,
                LoadedContentHash: loadedContentHash,
                VersionCatalogUpdated: versionCatalogUpdated,
                FailureMessage: null,
                RollbackFailure: null);
        }

        public static FlowVersionRestoreResult Failure(
            string failureMessage,
            string? rollbackFailure = null)
        {
            return new FlowVersionRestoreResult(
                Succeeded: false,
                LoadedContentHash: null,
                VersionCatalogUpdated: false,
                FailureMessage: failureMessage,
                RollbackFailure: rollbackFailure);
        }
    }

    /// <summary>
    /// Coordinates the policy sidecar and the legacy template save. It keeps
    /// UI refresh outside the persistence transaction, so a refresh failure
    /// cannot trigger an invalid rollback after the template has committed.
    /// </summary>
    internal sealed class FlowVersionRestoreService
    {
        private readonly IFlowExecutionPolicyStore policyStore;
        private readonly Action<FlowParam, FlowTemplateSaveCondition>
            saveTemplate;
        private readonly Action<
            FlowRevision,
            FlowExecutionPolicySaveRequest> validateProjection;

        public FlowVersionRestoreService()
            : this(
                FlowExecutionPolicyStoreProvider.Shared,
                (flowParam, condition) =>
                    TemplateFlow.Save2DB(flowParam, condition),
                FlowVersionRestoreProjection.Validate)
        {
        }

        internal FlowVersionRestoreService(
            IFlowExecutionPolicyStore policyStore,
            Action<FlowParam, FlowTemplateSaveCondition> saveTemplate,
            Action<FlowRevision, FlowExecutionPolicySaveRequest>
                validateProjection)
        {
            this.policyStore = policyStore
                ?? throw new ArgumentNullException(nameof(policyStore));
            this.saveTemplate = saveTemplate
                ?? throw new ArgumentNullException(nameof(saveTemplate));
            this.validateProjection = validateProjection
                ?? throw new ArgumentNullException(
                    nameof(validateProjection));
        }

        public FlowVersionRestoreResult Restore(
            FlowVersionRestoreRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            FlowParam flowParam = request.FlowParam
                ?? throw new ArgumentNullException(
                    nameof(request.FlowParam));
            FlowRevision revision = request.Revision
                ?? throw new ArgumentNullException(
                    nameof(request.Revision));
            if (string.IsNullOrWhiteSpace(flowParam.FlowKey))
            {
                return FlowVersionRestoreResult.Failure(
                    "当前流程没有稳定的 FlowKey，无法恢复版本。");
            }

            string flowKey = flowParam.FlowKey;
            if (!string.Equals(
                    revision.FlowKey,
                    flowKey,
                    StringComparison.Ordinal))
            {
                return FlowVersionRestoreResult.Failure(
                    "所选版本不属于当前流程，已拒绝恢复。");
            }
            string previousData = flowParam.DataBase64;
            int? previousTemplateRevision =
                flowParam.TemplateRevision;
            FlowExecutionPolicySnapshot? previousPolicy = null;
            FlowExecutionPolicySnapshot? savedPolicy = null;
            try
            {
                if (!policyStore.TryLoad(
                        flowKey,
                        out previousPolicy,
                        out string? policyFailure))
                {
                    return FlowVersionRestoreResult.Failure(
                        "读取当前执行策略失败，未开始恢复："
                        + policyFailure);
                }

                FlowExecutionPolicySaveRequest targetPolicy =
                    FlowVersionRestoreProjection.CreatePolicySaveRequest(
                        flowKey,
                        previousPolicy.Revision,
                        revision.SemanticDocument);
                validateProjection(revision, targetPolicy);
                NormalizedFlowExecutionPolicy normalizedPolicy =
                    FlowExecutionPolicyRules.Normalize(
                        flowKey,
                        targetPolicy.ErrorRoutes,
                        targetPolicy.RetryPolicies);
                if (!string.Equals(
                        previousPolicy.ContentHash,
                        normalizedPolicy.ContentHash,
                        StringComparison.Ordinal))
                {
                    savedPolicy = policyStore.Save(targetPolicy);
                }

                flowParam.DataBase64 = Convert.ToBase64String(
                    revision.FullSnapshot);
                // TemplateFlow uses this revision to restore the exact
                // semantic projection while creating the new revision.
                flowParam.TemplateRevision = revision.Revision;
                saveTemplate(
                    flowParam,
                    new FlowTemplateSaveCondition(
                        request.ExpectedContentHash));
                return FlowVersionRestoreResult.Success(
                    flowParam.LoadedContentHash,
                    flowParam.TemplateRevision.HasValue);
            }
            catch (Exception ex)
            {
                flowParam.DataBase64 = previousData;
                flowParam.TemplateRevision =
                    previousTemplateRevision;
                string? rollbackFailure = TryRestorePreviousPolicy(
                    flowKey,
                    previousPolicy,
                    savedPolicy);
                return FlowVersionRestoreResult.Failure(
                    ex.Message,
                    rollbackFailure);
            }
        }

        private string? TryRestorePreviousPolicy(
            string flowKey,
            FlowExecutionPolicySnapshot? previous,
            FlowExecutionPolicySnapshot? saved)
        {
            if (previous == null || saved == null)
                return null;
            try
            {
                policyStore.Save(
                    new FlowExecutionPolicySaveRequest(
                        flowKey,
                        saved.Revision,
                        previous.ErrorRoutes,
                        previous.RetryPolicies));
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
