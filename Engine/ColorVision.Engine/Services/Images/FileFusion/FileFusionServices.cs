using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.Services.Results;
using FlowEngineLib.Base;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ColorVision.Engine.Services.Images.FileFusion;

internal interface IFileFusionServices
{
    int ResolveBatchId(string serialNumber);
    FileFusionResult Execute(IReadOnlyList<string> files, FileFusionMode mode, CancellationToken cancellationToken);
    int SaveResult(MeasureResultImgModel model);
    void Publish(CVStartCFC action, string nodeId, int zIndex, MeasureResultImgModel model);
}

internal sealed class FileFusionServices : IFileFusionServices
{
    public static FileFusionServices Instance { get; } = new();

    public int ResolveBatchId(string serialNumber)
    {
        MeasureBatchModel? batch = BatchResultMasterDao.Instance.GetByNameOrCode(serialNumber);
        return batch?.Id > 0 ? batch.Id : throw new InvalidOperationException($"找不到流程批次：{serialNumber}");
    }

    public FileFusionResult Execute(IReadOnlyList<string> files, FileFusionMode mode, CancellationToken cancellationToken)
        => Core.FileFusion.Default.Execute(files, mode, cancellationToken);

    public int SaveResult(MeasureResultImgModel model) => MeasureImgResultDao.Instance.SaveAndReturnId(model);

    public void Publish(CVStartCFC action, string nodeId, int zIndex, MeasureResultImgModel model)
        => ResultMessageBus.Default.PublishPersisted(ResultRoutes.LocalFlow, ResultKinds.Image, string.Empty,
            "Fusion", action.SerialNumber, nodeId, zIndex, model.Id, 100);
}
