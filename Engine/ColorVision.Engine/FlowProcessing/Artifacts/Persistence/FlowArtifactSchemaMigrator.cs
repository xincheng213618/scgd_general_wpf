using SqlSugar;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.FlowProcessing.Artifacts.Persistence
{
    /// <summary>
    /// Explicit schema entry point. Constructing a store never mutates the
    /// database unless the caller opts into this migrator.
    /// </summary>
    public static class FlowArtifactSchemaMigrator
    {
        public static IReadOnlyList<string> TableNames { get; } =
            new[]
            {
                FlowArtifactTableNames.Blob,
                FlowArtifactTableNames.Revision,
                FlowArtifactTableNames.RevisionPart,
                FlowArtifactTableNames.Dependency,
                FlowArtifactTableNames.Reference,
            };

        internal static IReadOnlyList<Type> ModelTypes { get; } =
            new[]
            {
                typeof(FlowArtifactBlobModel),
                typeof(FlowArtifactRevisionModel),
                typeof(FlowArtifactRevisionPartModel),
                typeof(FlowArtifactDependencyModel),
                typeof(FlowArtifactReferenceModel),
            };

        public static void EnsureSchema(SqlSugarClient db)
        {
            ArgumentNullException.ThrowIfNull(db);
            if (db.CurrentConnectionConfig.DbType != DbType.MySql)
            {
                throw new NotSupportedException(
                    "共享流程 artifact 权威存储仅支持 MySQL。"
                    + "测试请使用 InMemoryFlowArtifactStore。");
            }

            foreach (Type modelType in ModelTypes)
                db.CodeFirst.InitTables(modelType);
        }
    }
}
