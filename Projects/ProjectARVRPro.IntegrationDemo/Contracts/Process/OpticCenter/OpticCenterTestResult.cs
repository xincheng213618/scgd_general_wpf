using ColorVision.Common.MVVM;

namespace ProjectARVRPro.Process.OpticCenter
{
    /// <summary>
    /// 光学中心测试结果，描述图像中心和光学中心的偏移、倾斜和旋转。
    /// </summary>
    public class OpticCenterTestResult : ViewModelBase
    {
        /// <summary>图像中心 X 方向倾斜或偏移角，单位 degree。</summary>
        public ObjectiveTestItem ImageCenterXTilt { get; set; }
        /// <summary>图像中心 Y 方向倾斜或偏移角，单位 degree。</summary>
        public ObjectiveTestItem ImageCenterYTilt { get; set; }
        /// <summary>图像中心旋转角，单位 degree。</summary>
        public ObjectiveTestItem ImageCenterRotation { get; set; }
        /// <summary>光学中心旋转角，单位 degree。</summary>
        public ObjectiveTestItem OptCenterRotation { get; set; }
        /// <summary>光学中心 X 方向倾斜或偏移角，单位 degree。</summary>
        public ObjectiveTestItem OptCenterXTilt { get; set; }
        /// <summary>光学中心 Y 方向倾斜或偏移角，单位 degree。</summary>
        public ObjectiveTestItem OptCenterYTilt { get; set; }
    }
}
