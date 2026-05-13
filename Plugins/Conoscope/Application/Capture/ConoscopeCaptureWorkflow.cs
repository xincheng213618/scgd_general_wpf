using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Conoscope.ApplicationServices.Capture
{
    internal sealed record ConoscopeFlowCaptureResult(FlowControlData? FlowResult, string? FilePath)
    {
        public bool Started => FlowResult != null;
        public bool Completed => FlowResult?.FlowStatus == FlowStatus.Completed;
        public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);
    }

    internal sealed record ConoscopeCameraCaptureResult(MsgRecord MessageRecord, MsgRecordState State, string ExposureSummary, string? FilePath)
    {
        public bool Succeeded => State == MsgRecordState.Success;
        public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);
    }

    internal static class ConoscopeCaptureWorkflow
    {
        public static async Task<ConoscopeFlowCaptureResult> RunFlowAsync(TemplateModel<FlowParam> flowTemplate)
        {
            FlowControlData? result = await FlowEngineManager.GetInstance().DisplayFlow.RunFlowAndWaitAsync(flowTemplate);
            if (result == null)
            {
                return new ConoscopeFlowCaptureResult(null, null);
            }

            string? filePath = result.FlowStatus == FlowStatus.Completed
                ? await WaitForFlowCvcieAsync(result)
                : null;
            return new ConoscopeFlowCaptureResult(result, filePath);
        }

        public static async Task<ConoscopeCameraCaptureResult> CaptureCameraAsync(DeviceCamera camera, CalibrationParam calibrationParam)
        {
            double[] exposureTimes = GetCameraExpTimes(camera);
            string exposureSummary = FormatExposureSummary(exposureTimes);

            MsgRecord msgRecord = camera.DService.GetData(
                exposureTimes,
                calibrationParam,
                new AutoExpTimeParam { Id = -1, Name = string.Empty },
                new TemplateJsonParam { Id = -1, Name = string.Empty });

            MsgRecordState state = await WaitForMsgRecordAsync(msgRecord);
            string? filePath = state == MsgRecordState.Success
                ? await WaitForCameraCvcieAsync(msgRecord)
                : null;

            return new ConoscopeCameraCaptureResult(msgRecord, state, exposureSummary, filePath);
        }

        public static async Task<MsgRecordState> WaitForMsgRecordAsync(MsgRecord msgRecord)
        {
            if (IsFinalState(msgRecord.MsgRecordState))
            {
                return msgRecord.MsgRecordState;
            }

            TaskCompletionSource<MsgRecordState> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler(object? sender, MsgRecordState state)
            {
                if (IsFinalState(state))
                {
                    taskCompletionSource.TrySetResult(state);
                }
            }

            msgRecord.MsgRecordStateChanged += Handler;
            try
            {
                if (IsFinalState(msgRecord.MsgRecordState))
                {
                    return msgRecord.MsgRecordState;
                }

                return await taskCompletionSource.Task;
            }
            finally
            {
                msgRecord.MsgRecordStateChanged -= Handler;
            }
        }

        public static int ReadMsgReturnInt(MsgReturn? msgReturn, string propertyName)
        {
            try
            {
                if (msgReturn?.Data == null)
                {
                    return 0;
                }

                dynamic data = msgReturn.Data;
                object? value = propertyName switch
                {
                    "MasterId" => data.MasterId,
                    "Port" => data.Port,
                    _ => null
                };

                return value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private static async Task<string?> WaitForFlowCvcieAsync(FlowControlData flowResult)
        {
            MeasureBatchModel? batch = null;
            if (!string.IsNullOrWhiteSpace(flowResult.SerialNumber))
            {
                batch = BatchResultMasterDao.Instance.GetByCode(flowResult.SerialNumber);
            }

            batch ??= FlowEngineManager.GetInstance().Batch;
            if (batch == null || batch.Id <= 0)
            {
                return null;
            }

            for (int i = 0; i < 10; i++)
            {
                string? filePath = FindCvcieFile(MeasureImgResultDao.Instance.GetAllByBatchId(batch.Id));
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    return filePath;
                }

                await Task.Delay(300);
            }

            return null;
        }

        private static async Task<string?> WaitForCameraCvcieAsync(MsgRecord msgRecord)
        {
            for (int i = 0; i < 8; i++)
            {
                int masterId = ReadMsgReturnInt(msgRecord.MsgReturn, "MasterId");
                if (masterId > 0)
                {
                    MeasureResultImgModel? result = MeasureImgResultDao.Instance.GetById(masterId);
                    string? filePath = GetCvcieFilePath(result);
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        return filePath;
                    }
                }

                await Task.Delay(300);
            }

            return null;
        }

        private static string? FindCvcieFile(IEnumerable<MeasureResultImgModel> results)
        {
            foreach (MeasureResultImgModel result in results)
            {
                string? filePath = GetCvcieFilePath(result);
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    return filePath;
                }
            }

            return null;
        }

        private static string? GetCvcieFilePath(MeasureResultImgModel? result)
        {
            if (result == null)
            {
                return null;
            }

            foreach (string? candidate in new[] { result.FileUrl, result.RawFile })
            {
                if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
                {
                    continue;
                }

                if (string.Equals(Path.GetExtension(candidate), ".cvcie", StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsFinalState(MsgRecordState state)
        {
            return state is MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout;
        }

        private static double[] GetCameraExpTimes(DeviceCamera camera)
        {
            return camera.Config.IsExpThree
                ? new[] { camera.DisplayConfig.ExpTimeR, camera.DisplayConfig.ExpTimeG, camera.DisplayConfig.ExpTimeB }
                : new[] { camera.DisplayConfig.ExpTime };
        }

        private static string FormatExposureSummary(double[] exposureTimes)
        {
            if (exposureTimes.Length <= 1)
            {
                return $"{exposureTimes[0].ToString("0.###", CultureInfo.InvariantCulture)} ms";
            }

            string[] channelNames = new[] { "R", "G", "B" };
            string channelSummary = string.Join(" / ", exposureTimes
                .Select((value, index) => $"{channelNames[Math.Min(index, channelNames.Length - 1)]}:{value.ToString("0.###", CultureInfo.InvariantCulture)}"));
            return $"{channelSummary} ms";
        }
    }
}