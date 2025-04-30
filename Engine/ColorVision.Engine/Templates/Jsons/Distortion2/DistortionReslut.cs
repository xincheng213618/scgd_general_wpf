using Newtonsoft.Json;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.Jsons.Distortion2
{

    public class DistortionReslut
    {
        [JsonProperty("Optic_Distortion")]
        public OpticDistortion OpticDistortion { get; set; }

        [JsonProperty("Point9_distortion")]
        public Point9Distortion Point9Distortion { get; set; }

        [JsonProperty("TV_distortion")]
        public TVDistortion TVDistortion { get; set; }
        public override string ToString() => JsonConvert.SerializeObject(this);
    }

    public class OpticDistortion
    {
        [JsonProperty("finalPoints")]
        public List<Point> FinalPoints { get; set; }

        [JsonProperty("maxErrPoint")]
        public Point MaxErrPoint { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("opticRatio")]
        public double OpticRatio { get; set; }

        [JsonProperty("t")]
        public double T { get; set; }
        public override string ToString() => JsonConvert.SerializeObject(this);
    }

    public class Point9Distortion
    {
        [JsonProperty("bottomRatio")]
        public double BottomRatio { get; set; }

        [JsonProperty("keyStoneHoriRatio")]
        public double KeyStoneHoriRatio { get; set; }

        [JsonProperty("keyStoneVercRatio")]
        public double KeyStoneVercRatio { get; set; }

        [JsonProperty("leftRatio")]
        public double LeftRatio { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("rightRatio")]
        public double RightRatio { get; set; }

        [JsonProperty("topRatio")]
        public double TopRatio { get; set; }
        public override string ToString() => JsonConvert.SerializeObject(this);
    }

    public class TVDistortion
    {
        [JsonProperty("finalPoints")]
        public List<Point> FinalPoints { get; set; }

        [JsonProperty("horizontalRatio")]
        public double HorizontalRatio { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("verticalRatio")]
        public double VerticalRatio { get; set; }

        public override string ToString() => JsonConvert.SerializeObject(this);
    }

    public class Point
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }
    }

}
