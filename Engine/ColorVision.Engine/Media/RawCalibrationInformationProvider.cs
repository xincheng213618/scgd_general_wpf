using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Settings;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColorVision.Engine.Media;

/// <summary>Projects validated file metadata into the editor's read-only information page.</summary>
public sealed class RawCalibrationInformationProvider : IImageComponent
{
    internal const string MetadataKey = "CVRaw.ColorCalibration";

    public void Execute(ImageView imageView)
    {
        imageView.RegisterSettings(() =>
        {
            if (imageView.Config.GetProperties<ColorCalibrationSnapshot>(MetadataKey) is not { } snapshot)
                return Array.Empty<ImageViewSettingsEntry>();

            return [new ImageViewSettingsEntry(SettingsText.Information, EngineLocalization.Get("色度校正参数"), snapshot)
            {
                Id = "cvraw-color-calibration", OwnerId = "Engine", CategoryId = ImageSettingsCategories.Information,
                Scope = ImageSettingsScope.CurrentImage, IsReadOnly = true, Order = 1,
                Description = EngineLocalization.Get("CVRAW 尾部记录的实际执行参数；查看和复制不会修改文件。系数已包含校正时的曝光、增益等归一化因素，不应再次叠加。"),
                CreateView = () => new ImageSettingsInformationView(() => GetRows(snapshot))
            }];
        });
    }

    internal static IEnumerable<(string Name, string Value)> GetRows(ColorCalibrationSnapshot snapshot)
    {
        static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        static string Values(IEnumerable<double> values) => string.Join(", ", values.Select(Number));
        string Row(int offset) => $"[ {Values(snapshot.Coefficients.Skip(offset).Take(3))} ]";

        yield return (EngineLocalization.Get("校正类型"), $"{(CalibrationType)snapshot.CalibrationType} ({snapshot.CalibrationType})");
        yield return (EngineLocalization.Get("校正模板"), snapshot.Template);
        yield return (EngineLocalization.Get("校正时间（UTC）"), snapshot.CalibratedUtc.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture));
        yield return (EngineLocalization.Get("可重放"), EngineLocalization.Get(snapshot.CanReplay ? "是" : "否（仅记录参数，当前 RAW 不能直接重放）"));
        yield return (EngineLocalization.Get("校正输入"), $"{snapshot.Width} × {snapshot.Height} px / {snapshot.RawBpp} bit / {snapshot.Channels} ch");
        yield return (EngineLocalization.Get("像素布局"), snapshot.Channels == 1 ? "Y" : snapshot.InterleavedBgr ? "BGR (interleaved)" : "R / G / B (planar)");
        yield return (EngineLocalization.Get("校正曝光"), string.Join(", ", snapshot.Exposure.Select(value => value.ToString("R", CultureInfo.InvariantCulture))));
        if (snapshot.TransformKind == 0)
        {
            yield return (EngineLocalization.Get("色度校正矩阵"), string.Join(Environment.NewLine, Row(0), Row(3), Row(6)));
            yield return (EngineLocalization.Get("矩阵含义"), "[X, Y, Z]ᵀ = M × [R, G, B]ᵀ");
        }
        else if (snapshot.TransformKind == 1)
        {
            yield return (EngineLocalization.Get("单色校正系数"), $"a = {Number(snapshot.Coefficients[0])}\nb = {Number(snapshot.Coefficients[1])}\nc = {Number(snapshot.Coefficients[2])}\nd = {Number(snapshot.Coefficients[3])}");
            yield return (EngineLocalization.Get("系数含义"), "X = a × R + d × B\nY = b × G\nZ = c × B");
        }
        else
        {
            yield return (EngineLocalization.Get("亮度校正系数"), Number(snapshot.Coefficients[0]));
            yield return (EngineLocalization.Get("系数含义"), "Y = k × RAW");
        }
        if (snapshot.SpectrumCorrection is { } correction)
        {
            yield return (EngineLocalization.Get("光谱参考修正"), $"{correction.Mode} / {correction.PointName}");
            yield return (EngineLocalization.Get("XYZ 修正倍率"), Values(correction.Factors));
        }
    }
}
