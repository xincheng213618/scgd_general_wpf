using ColorVision.Engine.Media;
using cvColorVision;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Services.PhyCameras.Calibration
{
    // The correction operates on already-calibrated XYZ. Keep the original file
    // separately: Gain/pa is not the Gain_x/Texp_x contract of manual RAW conversion.
    internal sealed class LumFourColorCalibrationFile
    {
        private static readonly string[] MatrixKeys = ["a", "b", "c", "d", "e", "f", "g", "h", "i"];
        private readonly JObject source;

        private LumFourColorCalibrationFile(JObject source, CVRawManualCieConfig config, CalibrationType type)
        {
            this.source = source;
            Config = config;
            CalibrationType = type;
        }

        public CVRawManualCieConfig Config { get; }
        public CalibrationType CalibrationType { get; }
        public string FormatDescription => CalibrationType switch
        {
            CalibrationType.Luminance => "亮度文件（a）",
            CalibrationType.LumMultiColor => "多色文件（Gain / pa）",
            _ => "四色文件（a…i）",
        };

        public static LumFourColorCalibrationFile Load(string path, bool luminanceOnly = false)
        {
            JObject root;
            try
            {
                string json = File.ReadAllText(path);
                root = luminanceOnly
                    ? Editing.CalibrationJsonDocument.ParseObject(json)
                    : JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            }
            catch (JsonException ex)
            {
                if (luminanceOnly)
                    throw new InvalidOperationException(EngineLocalization.Get("单通道亮度校正文件须为无重复字段的 JSON。"), ex);
                throw new InvalidOperationException("校正文件须为 a…i 或 Gain/pa 格式的 JSON，且不能包含重复字段。旧版非 JSON 文本暂不支持。", ex);
            }

            if (luminanceOnly)
            {
                if (root.ContainsKey("pa") || MatrixKeys.All(root.ContainsKey))
                    throw new InvalidOperationException(EngineLocalization.Get("请选择单通道亮度校正文件，当前文件包含色度校正矩阵。"));

                // The luminance and one-color formats can share the same keys. The operator confirms the resource slot.
                JToken? value = root["a"];
                if (value?.Type is not (JTokenType.Integer or JTokenType.Float)
                    || !double.IsFinite(value.Value<double>()) || value.Value<double>() <= 0)
                    throw new InvalidOperationException(EngineLocalization.Get("原亮度系数 a 必须是大于 0 的有限数值。"));

                return new LumFourColorCalibrationFile(root, new CVRawManualCieConfig { A = value.Value<double>() }, CalibrationType.Luminance);
            }

            if (root.ContainsKey("pa"))
            {
                if (MatrixKeys.Any(root.ContainsKey))
                    throw new InvalidOperationException("校正文件同时包含 pa 和 a…i，无法确定应使用哪套矩阵，请核对原文件。");
                if (root["pa"] is not JArray pa || pa.Count != 9)
                    throw new InvalidOperationException("校正文件的 pa 必须恰好包含 9 个矩阵系数。");
                double[] matrix = pa.Select((value, index) => ReadNumber(value, $"pa[{index}]")).ToArray();

                // Native LumMultiColor divides by the first three Gain entries.
                if (root["Gain"] is not JArray gain || gain.Count < 3)
                    throw new InvalidOperationException("多色校正文件的 Gain 必须包含至少 3 个通道增益。");
                for (int index = 0; index < gain.Count; index++)
                {
                    double value = ReadNumber(gain[index], $"Gain[{index}]");
                    if (index < 3 && value == 0)
                        throw new InvalidOperationException($"多色校正文件的 Gain[{index}] 不能为 0。");
                }

                return new LumFourColorCalibrationFile(root, new CVRawManualCieConfig
                {
                    A = matrix[0], B = matrix[1], C = matrix[2],
                    D = matrix[3], E = matrix[4], F = matrix[5],
                    G = matrix[6], H = matrix[7], I = matrix[8],
                }, CalibrationType.LumMultiColor);
            }

            if (!CVRawManualCieCalculator.TryParseLumFourColorConfig(root, out var config, out string? error))
                throw new InvalidOperationException(error);
            return new LumFourColorCalibrationFile(root, config, CalibrationType.LumFourColor);
        }

        public string SerializeCorrection(CVRawManualCieConfig corrected)
        {
            ArgumentNullException.ThrowIfNull(corrected);
            JObject output = (JObject)source.DeepClone();
            if (CalibrationType == CalibrationType.Luminance)
            {
                if (!double.IsFinite(corrected.A) || corrected.A <= 0)
                    throw new InvalidOperationException(EngineLocalization.Get("修正后的亮度系数必须是大于 0 的有限数值。"));
                output["a"] = corrected.A;
                return output.ToString(Formatting.Indented);
            }

            double[] matrix = [corrected.A, corrected.B, corrected.C, corrected.D, corrected.E, corrected.F, corrected.G, corrected.H, corrected.I];
            if (matrix.Any(value => !double.IsFinite(value)))
                throw new InvalidOperationException("修正矩阵必须是有限数值。");

            if (CalibrationType == CalibrationType.LumMultiColor)
                output["pa"] = new JArray(matrix);
            else
                for (int index = 0; index < MatrixKeys.Length; index++)
                    output[MatrixKeys[index]] = matrix[index];
            return output.ToString(Formatting.Indented);
        }

        private static double ReadNumber(JToken token, string name)
        {
            if (token.Type is not (JTokenType.Integer or JTokenType.Float))
                throw new InvalidOperationException($"校正文件的 {name} 必须是有限数字。");
            double value = token.Value<double>();
            if (!double.IsFinite(value))
                throw new InvalidOperationException($"校正文件的 {name} 必须是有限数字。");
            return value;
        }
    }
}
