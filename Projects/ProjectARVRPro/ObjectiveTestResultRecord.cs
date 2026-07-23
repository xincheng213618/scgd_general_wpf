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
        public bool HasW51 { get; set; }
        public bool HasW255 { get; set; }
        public bool HasFov { get; set; }
        public int DynamicTestCount { get; set; }
        public int DynamicPoiCount { get; set; }
        public DateTime CreateTime { get; set; } = DateTime.Now;
        public DateTime UpdateTime { get; set; } = DateTime.Now;

        [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
        public string ObjectiveTestResultJson { get; set; } = string.Empty;

        public static ObjectiveTestResultRecord Create(ProjectARVRReuslt result, ObjectiveTestResult objectiveTestResult)
        {
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
                HasW51 = objectiveTestResult?.W51TestResult != null,
                HasW255 = objectiveTestResult?.W255TestResult != null,
                HasFov = HasFovData(objectiveTestResult),
                DynamicTestCount = objectiveTestResult?.DynamicTestResults?.Sum(x => x.Value?.Count ?? 0) ?? 0,
                DynamicPoiCount = objectiveTestResult?.DynamicPoixyuvDatas?.Sum(x => x.Value?.Count ?? 0) ?? 0,
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now,
                ObjectiveTestResultJson = JsonConvert.SerializeObject(objectiveTestResult)
            };
        }

        private static bool HasFovData(ObjectiveTestResult? result)
        {
            if (result == null) return false;
            return HasFovData(result.W51TestResult?.HorizontalFieldOfViewAngle, result.W51TestResult?.VerticalFieldOfViewAngle, result.W51TestResult?.DiagonalFieldOfViewAngle)
                || HasFovData(result.W255TestResult?.HorizontalFieldOfViewAngle, result.W255TestResult?.VerticalFieldOfViewAngle, result.W255TestResult?.DiagonalFieldOfViewAngle)
                || result.FieldOfViewTestResults?.Values.Any(item =>
                    item != null && HasFovData(item.HorizontalFieldOfViewAngle, item.VerticalFieldOfViewAngle, item.DiagonalFieldOfViewAngle)) == true;
        }

        private static bool HasFovData(ObjectiveTestItem? horizontal, ObjectiveTestItem? vertical, ObjectiveTestItem? diagonal)
        {
            return HasValue(horizontal) || HasValue(vertical) || HasValue(diagonal);
        }

        private static bool HasValue(ObjectiveTestItem? item)
        {
            return item != null && (!string.IsNullOrWhiteSpace(item.TestValue) || Math.Abs(item.Value) > double.Epsilon);
        }
    }
}
