using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.MTF2;
using Newtonsoft.Json;
using ProjectARVRPro.Process.MTF.MTF07;

#pragma warning disable CA1707 // 测试点属性名需与既有MTF命名和导出字段保持一致

namespace ProjectARVRPro.Process.MTF.MTF07.MTFV
{
    public sealed class MTFV07ViewTestResult : MTFV07TestResult, IMTF07ViewTestResult
    {
        public MTFDetailViewReslut? MTFDetailViewReslut { get; set; }
    }

    public class MTFV07TestResult : ViewModelBase, IMTF07TestResult
    {
        public ObjectiveTestItem MTF_V_Center_0F { get; set; } = CreateItem(nameof(MTF_V_Center_0F));
        public ObjectiveTestItem MTF_V_LeftUp_0_7F { get; set; } = CreateItem(nameof(MTF_V_LeftUp_0_7F));
        public ObjectiveTestItem MTF_V_RightUp_0_7F { get; set; } = CreateItem(nameof(MTF_V_RightUp_0_7F));
        public ObjectiveTestItem MTF_V_LeftDown_0_7F { get; set; } = CreateItem(nameof(MTF_V_LeftDown_0_7F));
        public ObjectiveTestItem MTF_V_RightDown_0_7F { get; set; } = CreateItem(nameof(MTF_V_RightDown_0_7F));

        [JsonIgnore]
        public IReadOnlyList<ObjectiveTestItem> Items =>
        [
            MTF_V_Center_0F,
            MTF_V_LeftUp_0_7F,
            MTF_V_RightUp_0_7F,
            MTF_V_LeftDown_0_7F,
            MTF_V_RightDown_0_7F
        ];

        public bool TryGetItem(string itemName, out ObjectiveTestItem item)
        {
            item = itemName switch
            {
                nameof(MTF_V_Center_0F) => MTF_V_Center_0F,
                nameof(MTF_V_LeftUp_0_7F) => MTF_V_LeftUp_0_7F,
                nameof(MTF_V_RightUp_0_7F) => MTF_V_RightUp_0_7F,
                nameof(MTF_V_LeftDown_0_7F) => MTF_V_LeftDown_0_7F,
                nameof(MTF_V_RightDown_0_7F) => MTF_V_RightDown_0_7F,
                _ => null!
            };
            return item != null;
        }

        private static ObjectiveTestItem CreateItem(string name) => new() { Name = name, Unit = "%" };
    }
}
