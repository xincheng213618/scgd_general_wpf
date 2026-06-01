using ColorVision.Common.MVVM;

namespace ProjectARVRPro.Process.Distortion
{
    /// <summary>
    /// 畸变测试结果，包含 TV 畸变、光学畸变、九点畸变和梯形畸变。
    /// </summary>
    public class DistortionTestResult : ViewModelBase
    {
        /// <summary>水平 TV 畸变，表示水平方向几何畸变比例，单位 %。</summary>
        public ObjectiveTestItem HorizontalTVDistortion { get; set; }
        /// <summary>垂直 TV 畸变，表示垂直方向几何畸变比例，单位 %。</summary>
        public ObjectiveTestItem VerticalTVDistortion { get; set; }
        /// <summary>光学畸变，JSON 字段名为 Optic_Distortion，表示整体光学几何畸变，单位 %。</summary>
        public ObjectiveTestItem Optic_Distortion { get; set; }
        /// <summary>九点法上边畸变，单位 %。</summary>
        public ObjectiveTestItem DistortionTop { get; set; }
        /// <summary>九点法下边畸变，单位 %。</summary>
        public ObjectiveTestItem DistortionBottom { get; set; }
        /// <summary>九点法左边畸变，单位 %。</summary>
        public ObjectiveTestItem DistortionLeft { get; set; }
        /// <summary>九点法右边畸变，单位 %。</summary>
        public ObjectiveTestItem DistortionRight { get; set; }
        /// <summary>水平梯形畸变，表示画面左右宽度差异导致的梯形误差，单位 %。</summary>
        public ObjectiveTestItem KeystoneHoriz { get; set; }
        /// <summary>垂直梯形畸变，表示画面上下高度差异导致的梯形误差，单位 %。</summary>
        public ObjectiveTestItem KeystoneVert { get; set; }

        /// <summary>
        /// 光学畸变便捷属性。为了保持无第三方依赖，demo 不使用 Newtonsoft.Json 特性，实际 JSON 字段仍为 Optic_Distortion。
        /// </summary>
        public ObjectiveTestItem OpticDistortion
        {
            get { return Optic_Distortion; }
            set { Optic_Distortion = value; }
        }
    }
}
