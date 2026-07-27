using ColorVision.Database;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal static class LocalFlowResultPersistence
    {
        private const string DisabledResourceKey = "LocalFlowResultPersistence.Disabled";

        public static void Disable(CVStartCFC action)
        {
            ArgumentNullException.ThrowIfNull(action);
            action.RuntimeResources.Set(DisabledResourceKey, new PersistenceDisabled());
        }

        public static bool TryGetBatch(
            CVStartCFC action,
            [NotNullWhen(true)] out MeasureBatchModel? batch)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (action.RuntimeResources.TryGet(DisabledResourceKey, out PersistenceDisabled _))
            {
                batch = null;
                return false;
            }

            batch = BatchResultMasterDao.Instance.GetByNameOrCode(action.SerialNumber);
            if (batch == null || batch.Id <= 0)
                throw new InvalidOperationException($"找不到流程批次：{action.SerialNumber}");
            return true;
        }

        public static int SaveAlgorithmResult(
            CVStartCFC action,
            ViewResultAlgType resultType,
            int? templateId,
            string? templateName,
            string? imageFile,
            string? deviceCode,
            int zIndex,
            int totalTime,
            object parameters)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (!TryGetBatch(action, out MeasureBatchModel? batch))
                return -1;

            AlgResultMasterModel model = new()
            {
                TId = templateId,
                TName = templateName ?? string.Empty,
                ImgFile = NullIfEmpty(imageFile)!,
                ImgFileType = resultType,
                BatchId = batch.Id,
                Zindex = zIndex,
                Params = JsonConvert.SerializeObject(parameters),
                DeviceCode = NullIfEmpty(deviceCode)!,
                ResultCode = 0,
                Result = "ok",
                TotalTime = totalTime,
                CreateDate = DateTime.Now
            };
            int masterId = AlgResultMasterDao.Instance.SaveAndReturnId(model);
            if (masterId <= 0) throw new InvalidOperationException($"保存本地算法结果失败：{resultType}。");
            return masterId;
        }

        public static void DeleteAlgorithmResult(int masterId)
        {
            if (masterId > 0) _ = AlgResultMasterDao.Instance.DeleteById(masterId);
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

        private sealed class PersistenceDisabled
        {
        }
    }
}
