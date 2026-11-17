using ColorVision.Database;
using FlowEngineLib.Base;
using System;

namespace ColorVision.Engine.FlowProcessing;

internal sealed class FlowMeasurementBatch(string serialNumber, int batchId, string connectionString, long batchInsertStartedAt)
{
    private const string ResourceKey = "ColorVision.Engine.FlowMeasurementBatch";

    public static void Bind(FlowRuntimeResources resources, string serialNumber, int batchId, string connectionString, long batchInsertStartedAt) =>
        resources.Set(ResourceKey, new FlowMeasurementBatch(serialNumber, batchId, connectionString, batchInsertStartedAt));

    public static int? ResolveId(CVStartCFC action, Func<string, int?>? lookup = null)
    {
        if (action.RuntimeResources.TryGet<FlowMeasurementBatch>(ResourceKey, out var batch) && batch.Matches(action.SerialNumber))
            return batch.Id;

        lookup ??= static serialNumber => BatchResultMasterDao.Instance.GetByNameOrCode(serialNumber)?.Id;
        return lookup(action.SerialNumber);
    }

    private int Id { get; } = batchId;

    private bool Matches(string currentSerialNumber) => Id > 0
        && MySqlLocalServicesManager.CanReuseBatchFrom(batchInsertStartedAt)
        && string.Equals(serialNumber, currentSerialNumber, StringComparison.Ordinal)
        && string.Equals(connectionString, MySqlControl.GetConnectionString(), StringComparison.Ordinal);
}
