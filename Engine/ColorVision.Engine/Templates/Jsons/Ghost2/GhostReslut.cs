using Newtonsoft.Json;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.Jsons.Ghost2
{ 
    public class GhostReslut
    {
        [JsonProperty(nameof(Analysis))]
        public Analysis Analysis { get; set; }

        [JsonProperty(nameof(Bright))]
        public Bright Bright { get; set; }

        [JsonProperty(nameof(Ghost))]
        public Ghost Ghost { get; set; }
    }

    public class Analysis
    {
        [JsonProperty("ghost_bright_ratio")]
        public List<Ratio> GhostBrightRatio { get; set; }

        [JsonProperty("max&min")]
        public MaxMin MaxMin { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
        public override string ToString() => JsonConvert.SerializeObject(this);
    }

    public class Ratio
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("ratio")]
        public double RatioValue { get; set; }
    }

    public class MaxMin
    {
        [JsonProperty("maxGhostId")]
        public int MaxGhostId { get; set; }

        [JsonProperty("maxGhostRatio")]
        public double MaxGhostRatio { get; set; }

        [JsonProperty("minGhostId")]
        public int MinGhostId { get; set; }

        [JsonProperty("minGhostRatio")]
        public double MinGhostRatio { get; set; }
    }

    public class Bright
    {
        [JsonProperty("Y_Lum")]
        public List<Luminosity> YLum { get; set; }

        [JsonProperty("area")]
        public List<Area> Area { get; set; }

        [JsonProperty("center")]
        public List<Center> Center { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
        public override string ToString() => JsonConvert.SerializeObject(this);
    }

    public class Ghost
    {
        [JsonProperty("Y_Lum")]
        public List<Luminosity> YLum { get; set; }

        [JsonProperty("area")]
        public List<Area> Area { get; set; }

        [JsonProperty("center")]
        public List<Center> Center { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
        public override string ToString() => JsonConvert.SerializeObject(this);
    }

    public class Luminosity
    {
        [JsonProperty(nameof(Lum))]
        public double Lum { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }
    }

    public class Area
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("pixNum")]
        public int PixNum { get; set; }
    }

    public class Center
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }
    }

}
