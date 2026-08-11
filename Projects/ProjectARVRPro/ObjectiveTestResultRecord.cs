using ColorVision.Database;
using Newtonsoft.Json;
using SqlSugar;

namespace ProjectARVRPro
{
    [SugarTable("ObjectiveTestResultRecord")]
    public class ObjectiveTestResultRecord : ViewEntity
    {
        public int ResultId { get; set; }
        public int BatchId { get; set; }
        public string SN { get; set; } = string.Empty;
        public string LastCode { get; set; } = string.Empty;
        public string LastModel { get; set; } = string.Empty;
        public int LastTestType { get; set; }
        public string LastFlowStatus { get; set; } = string.Empty;
        public string Msg { get; set; } = string.Empty;
        public bool LastResult { get; set; }
        public bool TotalResult { get; set; }
        [SugarColumn(IsNullable = true)]
        public bool? IsFinalized { get; set; }
        public bool HasW51 { get; set; }
        public bool HasW255 { get; set; }
        public bool HasFov { get; set; }
        public int DynamicTestCount { get; set; }
        public int DynamicPoiCount { get; set; }
        public DateTime CreateTime { get; set; } = DateTime.Now;
        public DateTime UpdateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 当前流程或按需加载后的整组结果 JSON。数据库只保存 GZip 压缩字段。
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public string? ObjectiveTestResultJson { get; set; }

        public static ObjectiveTestResultRecord Create(ProjectARVRReuslt result, ObjectiveTestResult objectiveTestResult)
        {
            DateTime savedAt = DateTime.Now;
            DateTime sessionStartTime = objectiveTestResult?.SessionStartTime is DateTime startTime && startTime <= savedAt
                ? startTime
                : savedAt;
            return new ObjectiveTestResultRecord
            {
                ResultId = result.Id,
                BatchId = result.BatchId,
                SN = result.SN ?? string.Empty,
                LastCode = result.Code ?? string.Empty,
                LastModel = result.Model ?? string.Empty,
                LastTestType = result.TestType,
                LastFlowStatus = result.FlowStatus.ToString(),
                Msg = objectiveTestResult?.Msg ?? result.Msg ?? string.Empty,
                LastResult = result.Result,
                TotalResult = objectiveTestResult?.TotalResult ?? false,
                IsFinalized = false,
                CreateTime = sessionStartTime,
                UpdateTime = savedAt,
                ObjectiveTestResultJson = JsonConvert.SerializeObject(objectiveTestResult)
            };
        }
    }
}
