using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using Newtonsoft.Json;
using System.IO;

namespace ProjectARVRPro.Process.OpticCenter;

internal sealed record RgbCrossBatchInput(string JsonFile, string ImageFile, int MasterId);
internal interface IRgbCrossBatchResults { RgbCrossBatchInput Resolve(int batchId); }
internal sealed class RgbCrossBatchResults : IRgbCrossBatchResults
{
    public RgbCrossBatchInput Resolve(int batchId)
    {
        if (batchId <= 0) throw new InvalidDataException("自动读取十字需要有效批次。");
        return Select(AlgResultMasterDao.Instance.GetAllByBatchId(batchId), batchId,
            id => DeatilCommonDao.Instance.GetAllByPid(id));
    }

    internal static RgbCrossBatchInput Select(IEnumerable<AlgResultMasterModel> masters, int batchId,
        Func<int, List<DetailCommonModel>> loadDetails)
    {
        var matches = masters.Where(m => m.BatchId == batchId && m.ImgFileType == ViewResultAlgType.FindCross && m.version == "2.0").ToArray();
        if (matches.Length == 0) throw new InvalidDataException("当前批次没有十字 FindCross 2.0 结果。");
        if (matches.Length != 1) throw new InvalidDataException("当前批次存在多条十字结果；每个十字解析步骤应对应独立批次中的唯一结果。");
        var master = matches[0];
        if (master.ResultCode != 0) throw new InvalidDataException("十字计算失败或状态未知，不能回退到其他历史结果。");
        var details = loadDetails(master.Id);
        if (details.Count != 1 || details[0].PId != master.Id) throw new InvalidDataException("十字需要一条对应的文件路径明细。");
        var pointer = JsonConvert.DeserializeObject<ResultFile>(details[0].ResultJson);
        if (string.IsNullOrWhiteSpace(pointer?.ResultFileName) || !Path.IsPathFullyQualified(pointer.ResultFileName))
            throw new InvalidDataException("十字数据库明细缺少绝对结果路径。");
        return new(Path.GetFullPath(pointer.ResultFileName), master.ImgFile ?? "", master.Id);
    }
}
