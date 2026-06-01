using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Windows; // PoiPointResultModel

namespace ProjectARVRPro.Process.Distortion
{
    public class DistortionViewTestResult: DistortionTestResult
    {
        public List<Point> Points { get; set; } = new List<Point>();
    }

    public class DistortionTestResult : ViewModelBase
    {
        /// <summary>
        /// 水平TV畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem HorizontalTVDistortion { get; set; }

        /// <summary>
        /// 垂直TV畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem VerticalTVDistortion { get; set; }

        /// <summary>
        /// 光学畸变(%) 测试项
        /// </summary>
        [JsonProperty("Optic_Distortion")]
        public ObjectiveTestItem OpticDistortion { get; set; }

        /// <summary>
        /// 9点上畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem DistortionTop { get; set; }

        /// <summary>
        /// 9点下畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem DistortionBottom { get; set; }

        /// <summary>
        /// 9点左畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem DistortionLeft { get; set; }

        /// <summary>
        /// 9点右畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem DistortionRight { get; set; }

        /// <summary>
        /// 9点水平梯形畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem KeystoneHoriz { get; set; }

        /// <summary>
        /// 9点垂直梯形畸变(%) 测试项
        /// </summary>
        public ObjectiveTestItem KeystoneVert { get; set; }

    }
}
