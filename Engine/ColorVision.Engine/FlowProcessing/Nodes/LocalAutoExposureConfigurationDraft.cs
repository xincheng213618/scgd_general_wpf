using ColorVision.Engine.Services.Devices.Camera.Local;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace ColorVision.Engine.FlowProcessing.Nodes;

internal sealed class LocalAutoExposureConfigurationDraft(JObject document, JObject settings)
{
    [Category("曝光设置"), DisplayName("起始曝光 (ms)"), Description("自动曝光从这个时间开始搜索。"), PropertyEditorType]
    [DisplayFormat(DataFormatString = "{0:G}")]
    public double? StartTime { get => GetNumber("autoExpTimeBegin"); set => SetNumber("autoExpTimeBegin", value); }

    [Category("曝光设置"), DisplayName("目标饱和度 (%)"), Description("自动曝光要达到的图像亮度，占 AD 最大值的百分比。"), PropertyEditorType]
    [DisplayFormat(DataFormatString = "{0:G}")]
    public double? Saturation { get => GetNumber("autoExpSaturation"); set => SetNumber("autoExpSaturation", value); }

    [Category("曝光设置"), DisplayName("MinExposureTimeMs"), Description("自动曝光允许使用的最短时间，必须大于 0。"), PropertyEditorType]
    [DisplayFormat(DataFormatString = "{0:G}")]
    public double? MinTime { get => GetNumber("minExpTime"); set => SetNumber("minExpTime", value); }

    [Category("曝光设置"), DisplayName("MaxExposureTimeMs"), Description("自动曝光允许使用的最长时间，不小于最短曝光。"), PropertyEditorType]
    [DisplayFormat(DataFormatString = "{0:G}")]
    public double? MaxTime { get => GetNumber("maxExpTime"); set => SetNumber("maxExpTime", value); }

    [DisplayName("ADMax"), Description("传感器像素值的满量程，按图像位深设置。"), PropertyEditorType]
    [DisplayFormat(DataFormatString = "{0:G}")]
    public double? MaxAd { get => GetNumber("autoExpSatMaxAD"); set => SetNumber("autoExpSatMaxAD", value); }

    [DisplayName("FrequencySyncHz"), Description("曝光同步使用的频率，默认 -1。"), PropertyEditorType]
    [DisplayFormat(DataFormatString = "{0:G}")]
    public double? SyncFrequency { get => GetNumber("autoExpSyncFreq"); set => SetNumber("autoExpSyncFreq", value); }

    [DisplayName("MaxPercent"), Description("SDK 统计高亮像素使用的比例，按小数填写。"), PropertyEditorType]
    [DisplayFormat(DataFormatString = "{0:G}")]
    public double? MaxPercentage { get => GetNumber("autoExpMaxPecentage"); set => SetNumber("autoExpMaxPecentage", value); }

    [DisplayName("SaturationDifference"), Description("SDK 判断是否达到目标饱和度时使用的偏差参数。"), PropertyEditorType]
    [DisplayFormat(DataFormatString = "{0:G}")]
    public double? SaturationDeviation { get => GetNumber("autoExpSatDev"); set => SetNumber("autoExpSatDev", value); }

    [DisplayName("BurstThresholdMs"), Description("SDK 切换 Burst 采集时使用的曝光阈值。"), PropertyEditorType]
    [DisplayFormat(DataFormatString = "{0:G}")]
    public double? BurstThreshold { get => GetNumber("burstThreshold"); set => SetNumber("burstThreshold", value); }

    internal static bool TryCreate(string json, [NotNullWhen(true)] out LocalAutoExposureConfigurationDraft? draft, out string error)
    {
        draft = null;
        try
        {
            var document = JObject.Parse(json);
            if (document["expTimeCfg"] is not JObject settings)
            {
                error = EngineLocalization.Get("配置缺少 expTimeCfg。可恢复设备参数后重新配置。");
                return false;
            }
            draft = new(document, settings);
            error = string.Empty;
            return true;
        }
        catch (JsonException ex)
        {
            error = EngineLocalization.Format($"JSON 格式有误：{ex.Message}");
            return false;
        }
    }

    internal bool TryGetJson(out string json, out string error)
    {
        json = string.Empty;
        try
        {
            LocalCameraAutoExposure.NormalizeConfiguration(settings);
            json = document.ToString(Formatting.Indented);
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or OverflowException or InvalidCastException)
        {
            error = EngineLocalization.Get(ex.Message);
            return false;
        }
    }

    private double? GetNumber(string name) => settings[name] is { Type: JTokenType.Integer or JTokenType.Float } number
        ? (double)number
        : null;

    private void SetNumber(string name, double? value)
    {
        // Keep the existing token when the field has not changed, including its numeric precision.
        if (value == GetNumber(name)) return;
        settings[name] = value;
    }
}
