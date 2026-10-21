using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ColorVision.FlowPoi.Client
{
    public sealed class FlowPoiResponse
    {
        /// <summary>Parses a saved response with the same validation used by live calls.</summary>
        public static FlowPoiResponse Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("必须提供响应 JSON。", nameof(json));
            return FromJsonObject(JObject.Parse(json));
        }

        internal static FlowPoiResponse FromJsonObject(JObject json)
        {
            if (!string.Equals((string)json["EventName"], FlowPoiClient.Command, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace((string)json["MsgID"]))
                throw new InvalidDataException("响应必须包含 FlowRunPoi EventName 和非空 MsgID。");

            FlowPoiResponse response = json.ToObject<FlowPoiResponse>();
            if (response == null)
                throw new InvalidDataException("无法解析 FlowRunPoi 响应。");
            if (response.IsSuccess && (response.Data == null || response.Data.BatchId <= 0
                || response.Data.Poi == null || response.Data.Poi.Count == 0
                || (response.Data.Status != "Succeeded" && response.Data.Status != "SucceededWithWarnings")))
                throw new InvalidDataException("主程序返回成功码，但没有完整的成功批次和 POI 结果。");

            response.ResponseJson = json.ToString(Formatting.Indented);
            return response;
        }

        public string Version { get; set; }
        public string MsgID { get; set; }
        public string EventName { get; set; }
        public string SerialNumber { get; set; }

        [JsonProperty(Required = Required.Always)]
        public int Code { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Msg { get; set; }

        public FlowPoiData Data { get; set; }

        [JsonIgnore]
        public bool IsSuccess { get { return Code == 200; } }

        [JsonIgnore]
        public string ResponseJson { get; internal set; }
    }

    public sealed class FlowPoiData
    {
        [JsonProperty(Required = Required.Always)]
        public int FlowId { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string FlowName { get; set; }

        [JsonProperty(Required = Required.Always)]
        public int BatchId { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string ExecutionSerialNumber { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string Status { get; set; }

        [JsonProperty(Required = Required.Always)]
        public long TotalTime { get; set; }

        [JsonProperty(Required = Required.Always)]
        public List<FlowPoiPoint> Poi { get; set; }
    }

    public sealed class FlowPoiPoint
    {
        public int AlgorithmResultId { get; set; }
        public string AlgorithmName { get; set; }
        public int? PoiId { get; set; }
        public string Name { get; set; }
        public int PixelX { get; set; }
        public int PixelY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        [JsonProperty("X", Required = Required.Always)]
        public double X { get; set; }

        [JsonProperty("Y", Required = Required.Always)]
        public double Y { get; set; }

        [JsonProperty("Z", Required = Required.Always)]
        public double Z { get; set; }

        // Explicit wire names keep XYZ distinct from the chromaticity coordinates.
        [JsonProperty("x", Required = Required.Always)]
        public double ChromaticityX { get; set; }

        [JsonProperty("y", Required = Required.Always)]
        public double ChromaticityY { get; set; }

        [JsonProperty("u", Required = Required.Always)]
        public double U { get; set; }

        [JsonProperty("v", Required = Required.Always)]
        public double V { get; set; }

        [JsonProperty(Required = Required.Always)]
        public double CCT { get; set; }

        [JsonProperty(Required = Required.Always)]
        public double Wave { get; set; }
    }
}
