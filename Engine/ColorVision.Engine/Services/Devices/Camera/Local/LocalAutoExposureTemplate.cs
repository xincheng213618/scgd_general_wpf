using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal static class LocalAutoExposureTemplate
    {
        // Snapshot template values before the asynchronous camera command starts.
        internal static string BuildConfiguration(ParamBase template, PhyExpTimeCfg defaults)
        {
            JObject settings = template switch
            {
                AutoExpTimeParam v1 => new JObject
                {
                    ["autoExpFlag"] = v1.autoExpFlag, ["autoExpTimeBegin"] = v1.autoExpTimeBegin,
                    ["autoExpSyncFreq"] = v1.autoExpSyncFreq, ["autoExpSaturation"] = v1.autoExpSaturation,
                    ["autoExpSatMaxAD"] = v1.autoExpSatMaxAD, ["autoExpMaxPecentage"] = v1.autoExpMaxPecentage,
                    ["autoExpSatDev"] = v1.autoExpSatDev, ["maxExpTime"] = v1.maxExpTime,
                    ["minExpTime"] = v1.minExpTime, ["burstThreshold"] = v1.burstThreshold
                },
                TemplateJsonParam v2 => JObject.Parse(v2.JsonValue)["expTimeCfg"] as JObject
                    ?? throw new InvalidOperationException("自动曝光模板缺少 expTimeCfg。"),
                { Id: -2 } => JObject.FromObject(defaults),
                _ => throw new InvalidOperationException("请选择自动曝光模板。")
            };
            foreach (string name in new[] { "autoExpTimeBegin", "autoExpSyncFreq", "autoExpSaturation", "autoExpSatMaxAD",
                "autoExpMaxPecentage", "autoExpSatDev", "maxExpTime", "minExpTime", "burstThreshold" })
            {
                JToken? value = settings[name];
                if (value?.Type is not (JTokenType.Integer or JTokenType.Float) || !double.IsFinite(value.Value<double>()))
                    throw new InvalidOperationException($"自动曝光模板参数 {name} 无效。");
            }
            if (settings["autoExpFlag"]?.Type != JTokenType.Boolean
                || settings.Value<double>("minExpTime") <= 0
                || settings.Value<double>("maxExpTime") < settings.Value<double>("minExpTime")
                || settings.Value<double>("autoExpTimeBegin") <= 0)
                throw new InvalidOperationException("自动曝光模板的开关或曝光范围无效。");
            return new JObject { ["expTimeCfg"] = settings.DeepClone() }.ToString(Formatting.None);
        }
    }
}
