using ColorVision.Database;
using Newtonsoft.Json;
using SqlSugar;

namespace ProjectLUX
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
        public bool LastResult { get; set; }
        public bool TotalResult { get; set; }
        public bool HasW51AR { get; set; }
        public bool HasW255AR { get; set; }
        public bool HasW255 { get; set; }
        public bool HasFov { get; set; }
        public DateTime CreateTime { get; set; } = DateTime.Now;
        public DateTime UpdateTime { get; set; } = DateTime.Now;

        [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
        public string ObjectiveTestResultJson { get; set; } = string.Empty;

        public static ObjectiveTestResultRecord Create(ProjectLUXReuslt result, ObjectiveTestResult objectiveTestResult)
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
                LastResult = result.Result,
                TotalResult = objectiveTestResult?.TotalResult ?? false,
                HasW51AR = objectiveTestResult?.W51ARTestResult != null,
                HasW255AR = objectiveTestResult?.W255ARTestResult != null,
                HasW255 = objectiveTestResult?.W255TestResult != null,
                HasFov = HasFovData(objectiveTestResult),
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now,
                ObjectiveTestResultJson = JsonConvert.SerializeObject(objectiveTestResult)
            };
        }

        private static bool HasFovData(ObjectiveTestResult? result)
        {
            if (result == null) return false;
            return HasFovData(result.W51ARTestResult?.HorizontalFieldOfViewAngle, result.W51ARTestResult?.VerticalFieldOfViewAngle, result.W51ARTestResult?.DiagonalFieldOfViewAngle)
                || HasFovData(result.W255TestResult?.HorizontalFieldOfViewAngle, result.W255TestResult?.VerticalFieldOfViewAngle, result.W255TestResult?.DiagonalFieldOfViewAngle);
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
