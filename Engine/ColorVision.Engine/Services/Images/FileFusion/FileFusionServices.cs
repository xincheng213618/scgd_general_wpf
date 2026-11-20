using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using FlowEngineLib.Base;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ColorVision.Engine.Services.Images.FileFusion;

internal interface IFileFusionServices
{
    (int Id, MySqlConnectedEndpoint? Endpoint) ResolveBatch(CVStartCFC action);
    FileFusionResult Execute(IReadOnlyList<string> files, FileFusionMode mode, CancellationToken cancellationToken, FileFusionOptions options);
    int SaveResult(MySqlConnectedEndpoint? endpoint, MeasureResultImgModel model);
    void Publish(CVStartCFC action, string nodeId, int zIndex, MeasureResultImgModel model, MySqlConnectedEndpoint? endpoint);
}

internal sealed class FileFusionServices : IFileFusionServices
{
    public static FileFusionServices Instance { get; } = new();

    public (int Id, MySqlConnectedEndpoint? Endpoint) ResolveBatch(CVStartCFC action)
    {
        var endpoint = LocalFlowResultPersistence.ResolveEndpoint(action)
            ?? throw new InvalidOperationException("保存景深融合结果需要已连接的流程数据库。");
        return (LocalFlowResultPersistence.ResolveBatchId(action, endpoint), endpoint);
    }

    public FileFusionResult Execute(IReadOnlyList<string> files, FileFusionMode mode, CancellationToken cancellationToken, FileFusionOptions options)
        => Core.FileFusion.Default.Execute(files, mode, cancellationToken, options);

    public int SaveResult(MySqlConnectedEndpoint? endpoint, MeasureResultImgModel model) =>
        MeasureImgResultDao.InsertResult(endpoint ?? throw new ArgumentNullException(nameof(endpoint)), model);

    public void Publish(CVStartCFC action, string nodeId, int zIndex, MeasureResultImgModel model, MySqlConnectedEndpoint? endpoint)
        => ResultMessageBus.Default.PublishPersisted(ResultRoutes.LocalFlow, ResultKinds.Image, string.Empty,
            "Fusion", action.SerialNumber, nodeId, zIndex, model.Id, 100, endpoint);
}
