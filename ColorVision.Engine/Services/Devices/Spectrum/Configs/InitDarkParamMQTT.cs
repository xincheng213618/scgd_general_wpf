﻿using Newtonsoft.Json;

namespace ColorVision.Engine.Services.Devices.Spectrum.Configs
{
    public class InitDarkParamMQTT
    {
        [JsonProperty("fIntTime")]
        public float IntTime { get; set; }
        [JsonProperty("iAveNum")]
        public int AveNum { get; set; }
    }
}
