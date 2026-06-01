using ColorVision.Common.MVVM;
using ColorVision.FileIO;
using ColorVision.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;

namespace ColorVision.Engine.Media
{
    public sealed class CVRawManualCieConfig : ViewModelBase, IConfig
    {
        private const double DefaultA = 0.4123907992659595d;
        private const double DefaultB = 0.3575843393838780d;
        private const double DefaultC = 0.1804807884018343d;
        private const double DefaultD = 0.2126390058715104d;
        private const double DefaultE = 0.7151686787677560d;
        private const double DefaultF = 0.0721923153607337d;
        private const double DefaultG = 0.0193308187155919d;
        private const double DefaultH = 0.1191947797946260d;
        private const double DefaultI = 0.9505321522496607d;

        public static CVRawManualCieConfig Instance => ConfigService.Instance.GetRequiredService<CVRawManualCieConfig>();

        [Display(Name = nameof(Gain_x), GroupName = "Engine_PG_InputNormalization", Description = "Engine_PG_GainXDesc", ResourceType = typeof(Properties.Resources))]
        public double Gain_x { get; set; } = 1d;

        [Display(Name = nameof(Gain_y), GroupName = "Engine_PG_InputNormalization", Description = "Engine_PG_GainYDesc", ResourceType = typeof(Properties.Resources))]
        public double Gain_y { get; set; } = 1d;

        [Display(Name = nameof(Gain_z), GroupName = "Engine_PG_InputNormalization", Description = "Engine_PG_GainZDesc", ResourceType = typeof(Properties.Resources))]
        public double Gain_z { get; set; } = 1d;

        [Display(Name = nameof(Texp_x), GroupName = "Engine_PG_InputNormalization", Description = "Engine_PG_TexpXDesc", ResourceType = typeof(Properties.Resources))]
        public double Texp_x { get; set; }

        [Display(Name = nameof(Texp_y), GroupName = "Engine_PG_InputNormalization", Description = "Engine_PG_TexpYDesc", ResourceType = typeof(Properties.Resources))]
        public double Texp_y { get; set; }

        [Display(Name = nameof(Texp_z), GroupName = "Engine_PG_InputNormalization", Description = "Engine_PG_TexpZDesc", ResourceType = typeof(Properties.Resources))]
        public double Texp_z { get; set; }

        [Display(GroupName = "Engine_PG_MatrixXRow", ResourceType = typeof(Properties.Resources))]
        [DisplayName("a")]
        public double A { get; set; } = DefaultA;

        [Display(GroupName = "Engine_PG_MatrixXRow", ResourceType = typeof(Properties.Resources))]
        [DisplayName("b")]
        public double B { get; set; } = DefaultB;

        [Display(GroupName = "Engine_PG_MatrixXRow", ResourceType = typeof(Properties.Resources))]
        [DisplayName("c")]
        public double C { get; set; } = DefaultC;

        [Display(GroupName = "Engine_PG_MatrixYRow", ResourceType = typeof(Properties.Resources))]
        [DisplayName("d")]
        public double D { get; set; } = DefaultD;

        [Display(GroupName = "Engine_PG_MatrixYRow", ResourceType = typeof(Properties.Resources))]
        [DisplayName("e")]
        public double E { get; set; } = DefaultE;

        [Display(GroupName = "Engine_PG_MatrixYRow", ResourceType = typeof(Properties.Resources))]
        [DisplayName("f")]
        public double F { get; set; } = DefaultF;

        [Display(GroupName = "Engine_PG_MatrixZRow", ResourceType = typeof(Properties.Resources))]
        [DisplayName("g")]
        public double G { get; set; } = DefaultG;

        [Display(GroupName = "Engine_PG_MatrixZRow", ResourceType = typeof(Properties.Resources))]
        [DisplayName("h")]
        public double H { get; set; } = DefaultH;

        [Display(GroupName = "Engine_PG_MatrixZRow", ResourceType = typeof(Properties.Resources))]
        [DisplayName("i")]
        public double I { get; set; } = DefaultI;

        public static CVRawManualCieConfig CreateFactoryDefaults() => new();
    }

    internal static class CVRawManualCieCalculator
    {
        internal readonly record struct CalculationResult(byte[] XyzData, int Width, int Height, float[] Exposure);

        public static bool TryLoadLumFourColorCalibrationDefaults(string filePath, out CVRawManualCieConfig config, out string? errorMessage)
        {
            config = CVRawManualCieConfig.CreateFactoryDefaults();
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                JObject root = JObject.Parse(File.ReadAllText(filePath));
                return TryParseLumFourColorConfig(root, out config, out errorMessage);
            }
            catch (JsonException ex)
            {
                config = CVRawManualCieConfig.CreateFactoryDefaults();
                errorMessage = $"四色校正文件格式不正确: {Path.GetFileName(filePath)}。{ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                config = CVRawManualCieConfig.CreateFactoryDefaults();
                errorMessage = $"读取四色校正文件失败: {ex.Message}";
                return false;
            }
        }

        public static CalculationResult Calculate(CVCIEFile rawFile, CVRawManualCieConfig config)
        {
            ArgumentNullException.ThrowIfNull(rawFile);
            ArgumentNullException.ThrowIfNull(config);

            if (rawFile.FileExtType != CVType.Raw)
            {
                throw new InvalidOperationException("仅支持从 CVRAW 文件计算 CIE。");
            }

            if (rawFile.Channels != 3)
            {
                throw new InvalidOperationException("手动 CIE 仅支持三通道 CVRAW。");
            }

            if (rawFile.Data == null || rawFile.Data.Length == 0)
            {
                throw new InvalidOperationException("CVRAW 数据为空，无法计算 CIE。");
            }

            if (rawFile.Bpp != 8 && rawFile.Bpp != 16)
            {
                throw new InvalidOperationException("手动 CIE 当前仅支持 8bit 或 16bit 三通道 CVRAW。");
            }

            int pixelCount = checked(rawFile.Rows * rawFile.Cols);
            float[] exposure =
            {
                ResolveExposure(config.Texp_x, rawFile.Exp, 0),
                ResolveExposure(config.Texp_y, rawFile.Exp, 1),
                ResolveExposure(config.Texp_z, rawFile.Exp, 2),
            };

            double gainX = ResolveGain(config.Gain_x, rawFile.Gain);
            double gainY = ResolveGain(config.Gain_y, rawFile.Gain);
            double gainZ = ResolveGain(config.Gain_z, rawFile.Gain);

            double xFrom0 = config.A / exposure[0] / gainX;
            double xFrom1 = config.B / exposure[1] / gainY;
            double xFrom2 = config.C / exposure[2] / gainZ;
            double yFrom0 = config.D / exposure[0] / gainX;
            double yFrom1 = config.E / exposure[1] / gainY;
            double yFrom2 = config.F / exposure[2] / gainZ;
            double zFrom0 = config.G / exposure[0] / gainX;
            double zFrom1 = config.H / exposure[1] / gainY;
            double zFrom2 = config.I / exposure[2] / gainZ;

            float[] xyzPlanes = new float[checked(pixelCount * 3)];
            int yOffset = pixelCount;
            int zOffset = checked(pixelCount * 2);

            if (rawFile.Bpp == 8)
            {
                for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
                {
                    int sourceIndex = pixelIndex * 3;
                    double source0 = rawFile.Data[sourceIndex + 2];
                    double source1 = rawFile.Data[sourceIndex + 1];
                    double source2 = rawFile.Data[sourceIndex + 0];

                    xyzPlanes[pixelIndex] = (float)(xFrom0 * source0 + xFrom1 * source1 + xFrom2 * source2);
                    xyzPlanes[yOffset + pixelIndex] = (float)(yFrom0 * source0 + yFrom1 * source1 + yFrom2 * source2);
                    xyzPlanes[zOffset + pixelIndex] = (float)(zFrom0 * source0 + zFrom1 * source1 + zFrom2 * source2);
                }
            }
            else
            {
                ushort[] source = new ushort[checked(pixelCount * 3)];
                Buffer.BlockCopy(rawFile.Data, 0, source, 0, rawFile.Data.Length);

                for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
                {
                    int sourceIndex = pixelIndex * 3;
                    double source0 = source[sourceIndex + 2];
                    double source1 = source[sourceIndex + 1];
                    double source2 = source[sourceIndex + 0];

                    xyzPlanes[pixelIndex] = (float)(xFrom0 * source0 + xFrom1 * source1 + xFrom2 * source2);
                    xyzPlanes[yOffset + pixelIndex] = (float)(yFrom0 * source0 + yFrom1 * source1 + yFrom2 * source2);
                    xyzPlanes[zOffset + pixelIndex] = (float)(zFrom0 * source0 + zFrom1 * source1 + zFrom2 * source2);
                }
            }

            byte[] xyzData = new byte[checked(xyzPlanes.Length * sizeof(float))];
            Buffer.BlockCopy(xyzPlanes, 0, xyzData, 0, xyzData.Length);

            return new CalculationResult(xyzData, rawFile.Cols, rawFile.Rows, exposure);
        }

        private static float ResolveExposure(double configuredValue, float[]? sourceExposure, int index)
        {
            if (configuredValue > 0)
            {
                return (float)configuredValue;
            }

            if (sourceExposure != null)
            {
                if (index < sourceExposure.Length && sourceExposure[index] > 0)
                {
                    return sourceExposure[index];
                }

                if (sourceExposure.Length == 1 && sourceExposure[0] > 0)
                {
                    return sourceExposure[0];
                }
            }

            throw new InvalidOperationException($"第 {index + 1} 个输入通道曝光无效，请在计算窗口里填写大于 0 的曝光值。");
        }

        private static double ResolveGain(double configuredValue, float rawGain)
        {
            if (configuredValue > 0)
            {
                return configuredValue;
            }

            if (rawGain > 0)
            {
                return rawGain;
            }

            throw new InvalidOperationException("输入通道增益无效，请在计算窗口里填写大于 0 的增益值。");
        }

        private static bool TryParseLumFourColorConfig(JObject root, out CVRawManualCieConfig config, out string? errorMessage)
        {
            config = CVRawManualCieConfig.CreateFactoryDefaults();
            errorMessage = null;

            if (!TryReadRequiredDouble(root, nameof(CVRawManualCieConfig.Gain_x), out double gainX, out errorMessage)
                || !TryReadRequiredDouble(root, nameof(CVRawManualCieConfig.Gain_y), out double gainY, out errorMessage)
                || !TryReadRequiredDouble(root, nameof(CVRawManualCieConfig.Gain_z), out double gainZ, out errorMessage)
                || !TryReadRequiredDouble(root, nameof(CVRawManualCieConfig.Texp_x), out double texpX, out errorMessage)
                || !TryReadRequiredDouble(root, nameof(CVRawManualCieConfig.Texp_y), out double texpY, out errorMessage)
                || !TryReadRequiredDouble(root, nameof(CVRawManualCieConfig.Texp_z), out double texpZ, out errorMessage)
                || !TryReadRequiredDouble(root, "a", out double a, out errorMessage)
                || !TryReadRequiredDouble(root, "b", out double b, out errorMessage)
                || !TryReadRequiredDouble(root, "c", out double c, out errorMessage)
                || !TryReadRequiredDouble(root, "d", out double d, out errorMessage)
                || !TryReadRequiredDouble(root, "e", out double e, out errorMessage)
                || !TryReadRequiredDouble(root, "f", out double f, out errorMessage)
                || !TryReadRequiredDouble(root, "g", out double g, out errorMessage)
                || !TryReadRequiredDouble(root, "h", out double h, out errorMessage)
                || !TryReadRequiredDouble(root, "i", out double i, out errorMessage))
            {
                return false;
            }

            config = new CVRawManualCieConfig
            {
                Gain_x = gainX,
                Gain_y = gainY,
                Gain_z = gainZ,
                Texp_x = texpX,
                Texp_y = texpY,
                Texp_z = texpZ,
                A = a,
                B = b,
                C = c,
                D = d,
                E = e,
                F = f,
                G = g,
                H = h,
                I = i,
            };

            return true;
        }

        private static bool TryReadRequiredDouble(JObject root, string propertyName, out double value, out string? errorMessage)
        {
            value = 0d;
            errorMessage = null;

            if (!root.TryGetValue(propertyName, StringComparison.Ordinal, out JToken? token))
            {
                errorMessage = $"四色校正文件格式不正确，缺少字段 {propertyName}。";
                return false;
            }

            string text = token.ToString();
            if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value))
            {
                errorMessage = $"四色校正文件格式不正确，字段 {propertyName} 不是有效数字。";
                return false;
            }

            return true;
        }
    }
}