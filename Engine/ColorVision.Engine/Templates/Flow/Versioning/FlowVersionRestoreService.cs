using ColorVision.Engine.Templates.Flow;
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
        string? FailureMessage)
    {
        public static FlowVersionRestoreResult Success(
            string? loadedContentHash,
            bool versionCatalogUpdated)
        {
            return new FlowVersionRestoreResult(
                Succeeded: true,
                LoadedContentHash: loadedContentHash,
                VersionCatalogUpdated: versionCatalogUpdated,
                FailureMessage: null);
        }

        public static FlowVersionRestoreResult Failure(
            string failureMessage)
        {
            return new FlowVersionRestoreResult(
                Succeeded: false,
                LoadedContentHash: null,
                VersionCatalogUpdated: false,
                FailureMessage: failureMessage);
        }
    }

    /// <summary>
    /// Restores a legacy template snapshot as a new current revision.
    /// </summary>
    internal sealed class FlowVersionRestoreService
    {
        private readonly Action<FlowParam, FlowTemplateSaveCondition>
            saveTemplate;

        public FlowVersionRestoreService()
            : this(
                (flowParam, condition) =>
                    TemplateFlow.Save2DB(flowParam, condition))
        {
        }

        internal FlowVersionRestoreService(
            Action<FlowParam, FlowTemplateSaveCondition> saveTemplate)
        {
            this.saveTemplate = saveTemplate
                ?? throw new ArgumentNullException(nameof(saveTemplate));
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
            try
            {
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
                return FlowVersionRestoreResult.Failure(ex.Message);
            }
        }
    }
}
