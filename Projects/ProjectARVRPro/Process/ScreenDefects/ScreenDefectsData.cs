using Newtonsoft.Json;

namespace ProjectARVRPro.Process.ScreenDefects
{
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ScreenDefectData
    {
        [JsonProperty]
        public int Id { get; set; }

        [JsonProperty]
        public string Type { get; set; } = string.Empty;

        [JsonProperty]
        public double X { get; set; }

        [JsonProperty]
        public double Y { get; set; }

        [JsonProperty]
        public double Width { get; set; }

        [JsonProperty]
        public double Height { get; set; }

        [JsonProperty]
        public double Area { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? Contrast { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? MeanValue { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? LocalMean { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ScreenDefectsData
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? AvgBrightness { get; set; }

        [JsonProperty]
        public int DefectCount { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? GradeLevel { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? TimeStamp { get; set; }

        [JsonProperty]
        public List<ScreenDefectData> Defects { get; set; } = new();
    }
}
