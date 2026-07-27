using ColorVision.Database;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using System;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal static class LocalFlowResultPersistence
    {
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
            MeasureBatchModel batch = BatchResultMasterDao.Instance.GetByNameOrCode(action.SerialNumber)
                ?? throw new InvalidOperationException($"找不到流程批次：{action.SerialNumber}");
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
    }
}
