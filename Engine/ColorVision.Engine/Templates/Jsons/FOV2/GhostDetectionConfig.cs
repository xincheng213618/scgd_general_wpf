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
        static public string getDefaultJson()
        {
            var obj = new FovJson();
            return Newtonsoft.Json.JsonConvert.SerializeObject(obj);
        }

        public Pattern pattern { get; set; }
        public int threshold { get; set; } = 20000;
        public double DarkRatio { get; set; } = 0.5f;
        public double FovDist { get; set; } = 9576;
        public double cameraDegrees { get; set; } = 137;
        public bool HorizontalFov { get; set; } = true;
        public bool VerticalFov { get; set; } = true;
        public bool AnglesFov { get; set; } = true;

        public bool debug = false;

        public string debugPath = "result\\";

        public param_exactCorner ExactCorner = new();

    }

    public struct param_exactCorner
    {/*角点精定位参数*/
        public double qualityLevel = 0.04; //阈值
        public int cutWidth = 200;
        public int edge = 10;

        public param_exactCorner()
        {
        }
    };
}
