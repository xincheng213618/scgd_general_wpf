
namespace ColorVision.Engine.Templates.Jsons.FOV2
{
    public enum Pattern
    {
        Circle = 0,
        Rectangle = 1,
        matrix = 2,
    };

    public class FovJson
    {

        public Pattern pattern { get; set; }
        public int threshold { get; set; } = 20000;
        public double DarkRatio { get; set; } = 0.5f;
        public double FovDist { get; set; } = 9576;
        public double cameraDegrees { get; set; } = 137;
        public bool HorizontalFov { get; set; } = true;
        public bool VerticalFov { get; set; } = true;
        public bool AnglesFov { get; set; } = true;

        public bool debug { get; set; }

        public string debugPath { get; set; } ="result\\";

        public param_exactCorner ExactCorner { get; set; } = new param_exactCorner();

    }

    public class param_exactCorner
    {

        public double qualityLevel { get; set; } = 0.04; //阈值
        public int cutWidth { get; set; } = 200;
        public int edge { get; set; } = 10;
    };
}
