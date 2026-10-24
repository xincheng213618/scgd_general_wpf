using ColorVision.Database;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal interface ILocalFlowResultTransaction<TDetail> : IDisposable
        where TDetail : class, IEntity, new()
    {
        void Begin();
        int InsertMaster(AlgResultMasterModel model);
        int InsertDetails(IReadOnlyCollection<TDetail> details);
        void Commit();
        void Rollback();
    }

    internal sealed class SqlSugarLocalFlowResultTransaction<TDetail> : ILocalFlowResultTransaction<TDetail>
        where TDetail : class, IEntity, new()
    {
        private readonly SqlSugarClient db = MySqlControl.CreateDbClient();

        public void Begin() => db.Ado.BeginTran();

        public int InsertMaster(AlgResultMasterModel model) => db.Insertable(model).ExecuteReturnIdentity();

        public int InsertDetails(IReadOnlyCollection<TDetail> details) => db.Insertable(details.ToList()).ExecuteCommand();

        public void Commit() => db.Ado.CommitTran();

        public void Rollback() => db.Ado.RollbackTran();

        public void Dispose() => db.Dispose();
    }

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
            object parameters,
            int resultCode = 0,
            string result = "ok",
            string? resultVersion = null)
        {
            AlgResultMasterModel model = CreateAlgorithmResultModel(
                action, resultType, templateId, templateName, imageFile, deviceCode, zIndex, totalTime, parameters, resultCode, result, resultVersion);
            int masterId = AlgResultMasterDao.Instance.SaveAndReturnId(model);
            if (masterId <= 0) throw new InvalidOperationException($"保存本地算法结果失败：{resultType}。");
            return masterId;
        }

        public static int SaveAlgorithmResultWithDetails<TDetail>(
            CVStartCFC action,
            ViewResultAlgType resultType,
            int? templateId,
            string? templateName,
            string? imageFile,
            string? deviceCode,
            int zIndex,
            int totalTime,
            object parameters,
            Func<int, IReadOnlyCollection<TDetail>> createDetails,
            string? resultVersion = null)
            where TDetail : class, IEntity, new()
        {
            ArgumentNullException.ThrowIfNull(createDetails);
            AlgResultMasterModel model = CreateAlgorithmResultModel(
                action, resultType, templateId, templateName, imageFile, deviceCode, zIndex, totalTime, parameters, resultVersion: resultVersion);
            return SaveAlgorithmResultWithDetailsCore(
                model,
                resultType,
                createDetails,
                static () => new SqlSugarLocalFlowResultTransaction<TDetail>());
        }

        internal static int SaveAlgorithmResultWithDetailsCore<TDetail>(
            AlgResultMasterModel model,
            ViewResultAlgType resultType,
            Func<int, IReadOnlyCollection<TDetail>> createDetails,
            Func<ILocalFlowResultTransaction<TDetail>> transactionFactory)
            where TDetail : class, IEntity, new()
        {
            ArgumentNullException.ThrowIfNull(model);
            ArgumentNullException.ThrowIfNull(createDetails);
            ArgumentNullException.ThrowIfNull(transactionFactory);
            using ILocalFlowResultTransaction<TDetail> transaction = transactionFactory()
                ?? throw new InvalidOperationException("无法创建本地算法结果数据库事务。");
            transaction.Begin();
            try
            {
                int masterId = transaction.InsertMaster(model);
                if (masterId <= 0)
                    throw new InvalidOperationException($"保存本地算法结果主表失败：{resultType}。");

                List<TDetail> details = createDetails(masterId)?.ToList()
                    ?? throw new InvalidOperationException($"本地算法结果明细为空：{resultType}。");
                if (details.Count == 0)
                    throw new InvalidOperationException($"本地算法结果没有可保存的明细：{resultType}。");
                int inserted = transaction.InsertDetails(details);
                if (inserted != details.Count)
                    throw new InvalidOperationException($"保存本地算法结果明细失败：应写入 {details.Count} 条，实际写入 {inserted} 条。");

                transaction.Commit();
                return masterId;
            }
            catch (Exception saveException)
            {
                try
                {
                    transaction.Rollback();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException("保存本地算法结果失败，且数据库事务回滚失败。", saveException, rollbackException);
                }
                throw;
            }
        }

        public static void DeleteAlgorithmResult(int masterId)
        {
            if (masterId > 0) _ = AlgResultMasterDao.Instance.DeleteById(masterId);
        }

        private static AlgResultMasterModel CreateAlgorithmResultModel(
            CVStartCFC action,
            ViewResultAlgType resultType,
            int? templateId,
            string? templateName,
            string? imageFile,
            string? deviceCode,
            int zIndex,
            int totalTime,
            object parameters,
            int resultCode = 0,
            string result = "ok",
            string? resultVersion = null)
        {
            ArgumentNullException.ThrowIfNull(action);
            ArgumentException.ThrowIfNullOrWhiteSpace(result);
            MeasureBatchModel batch = BatchResultMasterDao.Instance.GetByNameOrCode(action.SerialNumber)
                ?? throw new InvalidOperationException($"找不到流程批次：{action.SerialNumber}");
            var model = new AlgResultMasterModel
            {
                TId = templateId,
                TName = templateName ?? string.Empty,
                ImgFile = NullIfEmpty(imageFile),
                ImgFileType = resultType,
                version = resultVersion,
                BatchId = batch.Id,
                Zindex = zIndex,
                Params = JsonConvert.SerializeObject(parameters),
                DeviceCode = NullIfEmpty(deviceCode),
                ResultCode = resultCode,
                Result = result,
                TotalTime = totalTime,
                CreateDate = DateTime.Now
            };
            // Preserve this execution's upstream measurement; live IVL must not infer it from row counts.
            if (action.Data.TryGetValue("SMUResult", out var value) && value is FlowEngineLib.SMUResultData { MasterId: > 0, MasterResultType: 200 } smu)
            {
                model.SMUDataID = smu.MasterId;
                model.VResult = double.IsFinite(smu.V) ? (float)smu.V : null;
                model.IResult = double.IsFinite(smu.I) ? (float)smu.I : null;
            }
            return model;
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
