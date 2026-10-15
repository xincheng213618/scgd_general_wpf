using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.Templates.POI.BuildPoi;
using CVCommCore.CVAlgorithm;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ColorVision.Engine.FlowProcessing.Nodes
{
    internal sealed class LocalRealPoiParameters
    {
        public required PoiParam Poi { get; init; }
        public int SourceMasterId { get; init; } = -1;
    }

    internal static class LocalRealPoiInputResolver
    {
        public static LocalRealPoiParameters Resolve(int inputMasterId, int inputResultType, string imageInputName)
        {
            if (inputMasterId <= 0)
            {
                throw new InvalidOperationException("IN_POI 没有有效的布点结果，请连接上游关注点布点节点。");
            }
            if (inputResultType is (int)CVCommCore.CVResultType.Camera_Img
                or (int)CVCommCore.CVResultType.Algorithm_Calibration)
            {
                throw new InvalidOperationException($"IN_POI 接收到的是图像结果：MasterId={inputMasterId}，ResultType={inputResultType}。当前两条输入线可能接反；图像应连接 {imageInputName}，关注点布点应连接 IN_POI。");
            }
            return new LocalRealPoiParameters
            {
                Poi = BuildPoiFromInput(inputMasterId, inputResultType),
                SourceMasterId = inputMasterId
            };
        }

        private static PoiParam BuildPoiFromInput(int masterId, int masterResultType)
        {
            List<PoiPointResultModel> details = PoiPointResultDao.Instance.GetAllByPid(masterId);
            PoiParam poi = new() { Id = masterId, Name = $"IN_POI#{masterId}" };
            foreach (PoiPointResultModel detail in details)
            {
                poi.PoiPoints.Add(new PoiPoint
                {
                    Id = detail.PoiId ?? detail.Id,
                    Name = string.IsNullOrWhiteSpace(detail.PoiName) ? (detail.PoiId ?? detail.Id).ToString() : detail.PoiName,
                    PointType = detail.PoiType.ToPoiShape(),
                    PixX = detail.PoiX ?? 0,
                    PixY = detail.PoiY ?? 0,
                    PixWidth = Math.Max(detail.PoiWidth ?? 1, 1),
                    PixHeight = Math.Max(detail.PoiHeight ?? 1, 1)
                });
            }
            if (poi.PoiPoints.Count > 0) return poi;

            List<PoiCieFileModel> files = PoiCieFileDao.Instance.GetAllByPid(masterId);
            foreach (PoiCieFileModel file in files)
            {
                if (string.IsNullOrWhiteSpace(file.FileUrl) || !File.Exists(file.FileUrl)) continue;
                POIPointInfo? pointInfo = ViewHandleBuildPoiFile.ReadPOIPointFromCSV(file.FileUrl);
                if (pointInfo?.Positions == null || pointInfo.HeaderInfo == null) continue;
                int pointId = poi.PoiPoints.Count + 1;
                foreach (POIPointPosition position in pointInfo.Positions)
                {
                    poi.PoiPoints.Add(new PoiPoint
                    {
                        Id = pointId,
                        Name = pointId.ToString(),
                        PointType = pointInfo.HeaderInfo.PointType.ToPoiShape(),
                        PixX = position.PixelX,
                        PixY = position.PixelY,
                        PixWidth = Math.Max(pointInfo.HeaderInfo.Width, 1),
                        PixHeight = Math.Max(pointInfo.HeaderInfo.Height, 1)
                    });
                    pointId++;
                }
                poi.PoiConfig.AreaRectRow = pointInfo.HeaderInfo.Rows;
                poi.PoiConfig.AreaRectCol = pointInfo.HeaderInfo.Cols;
            }
            if (poi.PoiPoints.Count == 0)
            {
                throw new InvalidOperationException($"IN_POI 无法加载布点数据：MasterId={masterId}，ResultType={masterResultType}；数据库明细和布点文件均为空。");
            }
            return poi;
        }
    }

    internal sealed class LocalRealPoiNodeResultData
    {
        public string FrameId { get; init; } = string.Empty;
        public int MasterId { get; init; }
        public int MasterResultType { get; init; } = (int)ViewResultAlgType.POI_XYZ;
        public int CieMasterId { get; init; }
        public int PoiSourceMasterId { get; init; }
        public string PoiTemplateName { get; init; } = string.Empty;
        public int PointCount { get; init; }
        public int TotalTime { get; init; }
        public object? POIResult { get; init; }
    }

    [STNode("Flow_CustomNodes", "实时 POI", CategoryOrder = 9900)]
    public sealed class LocalRealPoiNode : LocalFlowNodeBase
    {
        private static readonly string[] InputPortNames = { "IN_CIE", "IN_POI" };

        public LocalRealPoiNode() : base("实时 POI", "LocalRealPOI", "Real_POI", InputPortNames)
        {
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            bool loadedFromFile = !action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame) || currentFrame == null;
            if (loadedFromFile)
            {
                string path = ResolveInputImageFilePath(action, 0, string.Empty);
                if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("图像输入端没有 CIE、带校正参数的 RAW 或图像结果文件。");
                currentFrame = FlowNodeTiming.Run("OpenImage", () => LocalFrameFileService.Load(path));
                _ = TryGetInputMasterResult(action, 0, out int imageMasterId, out _, out _);
                currentFrame.MasterId = imageMasterId;
            }
            try
            {
                string? imagePath = string.IsNullOrWhiteSpace(currentFrame!.CvCieFilePath) ? currentFrame.CvRawFilePath : currentFrame.CvCieFilePath;
                _ = TryGetInputMasterResult(action, 1, out int poiInputMasterId, out int poiInputResultType, out _);
                LocalRealPoiParameters parameters = LocalRealPoiInputResolver.Resolve(poiInputMasterId, poiInputResultType, InputPortNames[0]);

                Stopwatch stopwatch = Stopwatch.StartNew();
                LocalPoiResultSet result;
                using (LocalFlowFrameLease frame = currentFrame.Acquire())
                {
                    result = LocalPoiCalculator.Calculate(frame, parameters.Poi);
                }
                stopwatch.Stop();
                int totalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                ViewResultAlgType resultType = LocalPoiCalculator.ResolveResultType(currentFrame.Metadata.Channels);
                int masterId = -1;
                try
                {
                    masterId = LocalFlowResultPersistence.SaveAlgorithmResult(
                        action,
                        resultType,
                        parameters.Poi.Id,
                        parameters.Poi.Name,
                        imagePath,
                        null,
                        ZIndex,
                        totalTime,
                        new
                        {
                            CieMasterId = currentFrame.MasterId,
                            POISourceMasterId = parameters.SourceMasterId > 0 ? (int?)parameters.SourceMasterId : null,
                            CalibrationTemplate = currentFrame.Metadata.CalibrationTemplate,
                            POITemplate = parameters.Poi.Name,
                            FlipMode = currentFrame.Metadata.FlipMode.ToString(),
                            FlipApplied = currentFrame.IsFlipApplied,
                            ImageRead = loadedFromFile,
                            InputBuffer = currentFrame.HasCie ? "CIE" : "RAW",
                            MemoryOnly = string.IsNullOrWhiteSpace(imagePath)
                        });
                    LocalPoiCalculator.SaveDetails(masterId, result);

                    action.RuntimeResources.Set(LocalFlowFrameRuntime.GetPoiResultResourceKey(currentFrame.FrameId), result);
                    action.Data["LocalPoiCount"] = result.Points.Count;
                    action.Data["LocalPoiSourceMasterId"] = parameters.SourceMasterId;
                    action.MasterValue(null, masterId, (int)resultType);
                    ResultMessageBus.Default.PublishPersisted(ResultRoutes.LocalFlow, ResultKinds.Algorithm, string.Empty, OperatorCode, action.SerialNumber, NodeID, ZIndex, masterId, (int)resultType);
                    action.SetCurrentFrame(currentFrame);
                    loadedFromFile = false;
                    return new LocalNodeExecutionResult
                    {
                        Data = new LocalRealPoiNodeResultData
                        {
                            FrameId = currentFrame.FrameId.ToString("N"),
                            MasterId = masterId,
                            MasterResultType = (int)resultType,
                            CieMasterId = currentFrame.MasterId,
                            PoiSourceMasterId = parameters.SourceMasterId,
                            PoiTemplateName = result.TemplateName,
                            PointCount = result.Points.Count,
                            TotalTime = totalTime,
                            POIResult = result.Points
                        }
                    };
                }
                catch
                {
                    LocalPoiCalculator.DeleteDetails(masterId);
                    LocalFlowResultPersistence.DeleteAlgorithmResult(masterId);
                    throw;
                }
            }
            finally
            {
                if (loadedFromFile) currentFrame?.Dispose();
            }
        }

        protected override string BuildRunPayload(CVStartCFC action)
        {
            return JsonConvert.SerializeObject(new
            {
                ServiceName = NodeName,
                EventName = OperatorCode,
                action.SerialNumber,
                InputMode = "CurrentFrameThenInputFile",
                InputPorts = InputPortNames
            });
        }
    }
}
