using ColorVision.Engine.Templates.Jsons.Distortion2;
using System.Collections.Generic;
using System.ComponentModel;

namespace ProjectARVRPro.Process.Distortion
{
    public enum DistortionPointSource
    {
        [Description("TV")]
        TV,
        [Description("Point9")]
        Point9,
        [Description("Optic")]
        Optic
    }

    internal static class DistortionPointSourceHelper
    {
        public static IReadOnlyList<Point> GetPoints(DistortionReslut distortionResult, DistortionPointSource pointSource)
        {
            if (distortionResult == null)
                return null;

            return pointSource switch
            {
                DistortionPointSource.Point9 => distortionResult.Point9Distortion?.FinalPoints,
                DistortionPointSource.Optic => distortionResult.OpticDistortion?.FinalPoints,
                _ => distortionResult.TVDistortion?.FinalPoints,
            };
        }
    }

    public class DistortionProcessConfig : ProcessConfigBase
    {
        [Category("显示配置")]
        [DisplayName("9点来源")]
        [Description("结果图绘制使用的9点来源")]
        public DistortionPointSource PointSource { get => _PointSource; set { _PointSource = value; OnPropertyChanged(); } }
        private DistortionPointSource _PointSource = DistortionPointSource.TV;
    }
}
