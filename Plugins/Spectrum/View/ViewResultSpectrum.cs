using ColorVision.Common.MVVM;
using cvColorVision;
using Newtonsoft.Json;
using ScottPlot;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WpfColor = System.Windows.Media.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;

namespace Spectrum
{

    public class ViewResultSpectrum : ViewModelBase
    {
        private static int No = 1;

        [DisplayName("序号")]
        public int Id { get; set; }
        [DisplayName("测量时间")]
        public DateTime? CreateTime { get; set; }
        public string Batch { get; set; }
        public int? BatchID { get; set; }
        [JsonIgnore]
        public Scatter ScatterPlot { get; set; }
        [JsonIgnore]
        public Scatter AbsoluteScatterPlot { get; set; }

        public ObservableCollection<SpectralData> SpectralDatas { get; set; } = new ObservableCollection<SpectralData>();

        public void Gen()
        {

            IP = Math.Round(fIp / 65535 * 100, 2).ToString() + "%";

            Lv = (fPh / 1).ToString();

            double sum1 = 0, sum2 = 0;
            for (int i = 35; i <= 75; i++)
                sum1 += fPL[i * 10];
            for (int i = 20; i <= 120; i++)
                sum2 += fPL[i * 10];
            Blue = Math.Round(sum1 / sum2 * 100, 2).ToString();

            if (SpectralDatas.Count == 0)
            {
                for (int i = 0; i <= (780 - 380) * 10; i += 10)
                {
                    SpectralData SpectralData = new SpectralData();
                    SpectralData.Wavelength = i / 10 + 380;

                    SpectralData.RelativeSpectrum = fPL[i]>0? fPL[i] : 0;

                    SpectralData.AbsoluteSpectrum = fPL[i] * fPlambda;
                    SpectralDatas.Add(SpectralData);
                }
            }

            fSpect1 = 380;

            double[] xs = new double[fPL.Length];
            double[] ys = new double[fPL.Length];
            double[] ysAbsolute = new double[fPL.Length];
            for (int i = 0; i < fPL.Length; i++)
            {
                xs[i] = ((double)fSpect1 + Math.Round(fInterval, 1) * i);
                ys[i] = fPL[i];
                ysAbsolute[i] = fPL[i] * fPlambda;
            }

            ScatterSourceDoubleArray source = new(xs, ys);
            ScatterPlot = new Scatter(source)
            {
                Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod),
                LineWidth = 1,
                MarkerSize = 1,
                LegendText = string.Empty,
                MarkerShape = MarkerShape.None,
            };

            ScatterSourceDoubleArray sourceAbsolute = new(xs, ysAbsolute);
            AbsoluteScatterPlot = new Scatter(sourceAbsolute)
            {
                Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod),
                LineWidth = 1,
                MarkerSize = 1,
                LegendText = string.Empty,
                MarkerShape = MarkerShape.None,
            };

            // Compute dominant wavelength color
            DominantWavelengthColor = WavelengthToColor.ToBrush(fLd);
            DominantWavelengthHex = WavelengthToColor.ToHex(fLd);

            // Compute excitation purity
            ComputeExcitationPurity();
        }
        public ViewResultSpectrum()
        {

        }

        public ViewResultSpectrum(COLOR_PARA colorParam)
        {
            Id = No++;
            CreateTime = DateTime.Now;
            fCIEx = colorParam.fCIEx;
            fCIEy = colorParam.fCIEy;
            fCIEz = colorParam.fCIEz;
            fCIEx2015 = colorParam.fCIEx_2015;
            fCIEy2015 = colorParam.fCIEy_2015;
            fCIEz2015 = colorParam.fCIEz_2015;
            fx2015 = colorParam.fx_2015;
            fy2015 = colorParam.fy_2015;
            fu2015 = colorParam.fu_2015;
            fv2015 = colorParam.fv_2015;


            fx = colorParam.fx;
            fy = colorParam.fy;
            fu = colorParam.fu;
            fv = colorParam.fv;


            fCCT = colorParam.fCCT;
            dC = colorParam.dC;
            fLd = colorParam.fLd;
            fPur = colorParam.fPur;
            fLp = colorParam.fLp;
            fHW = colorParam.fHW;
            fLav = colorParam.fLav;
            dC = colorParam.dC;


            fRa = colorParam.fRa;
            fRR = colorParam.fRR;
            fGR = colorParam.fGR;
            fBR = colorParam.fBR;
            fRi = colorParam.fRi;
            fIp = colorParam.fIp;
            fPh = colorParam.fPh;
            fPhe = colorParam.fPhe;
            fPlambda = colorParam.fPlambda;
            fSpect1 = colorParam.fSpect1;
            fSpect2 = colorParam.fSpect2;
            fInterval = colorParam.fInterval;
            fPL = colorParam.fPL;
            Gen();
        }

        public ViewResultSpectrum(SprectrumModel sprectrumModel)
        {
            Id = sprectrumModel.Id;
            CreateTime = sprectrumModel.CreateTime;

            var colorParam = sprectrumModel.ColorParam;

            fCIEx = colorParam.fCIEx;
            fCIEy = colorParam.fCIEy;
            fCIEz = colorParam.fCIEz;
            fCIEx2015 = colorParam.fCIEx_2015;
            fCIEy2015 = colorParam.fCIEy_2015;
            fCIEz2015 = colorParam.fCIEz_2015;
            fx2015 = colorParam.fx_2015;
            fy2015 = colorParam.fy_2015;
            fu2015 = colorParam.fu_2015;
            fv2015 = colorParam.fv_2015;


            fx = colorParam.fx;
            fy = colorParam.fy;
            fu = colorParam.fu;
            fv = colorParam.fv;


            fCCT = colorParam.fCCT;
            dC = colorParam.dC;
            fLd = colorParam.fLd;
            fPur = colorParam.fPur;
            fLp = colorParam.fLp;
            fHW = colorParam.fHW;
            fLav = colorParam.fLav;
            dC = colorParam.dC;


            fRa = colorParam.fRa;
            fRR = colorParam.fRR;
            fGR = colorParam.fGR;
            fBR = colorParam.fBR;
            fRi = colorParam.fRi;
            fIp = colorParam.fIp;
            fPh = colorParam.fPh;
            fPhe = colorParam.fPhe;
            fPlambda = colorParam.fPlambda;
            fSpect1 = colorParam.fSpect1;
            fSpect2 = colorParam.fSpect2;
            fInterval = colorParam.fInterval;
            fPL = colorParam.fPL;
            Gen();
        }

        // EQE properties
        public double? Eqe { get; set; }

        [DisplayName("光通量(lm)")]
        public float? LuminousFlux { get; set; }

        [DisplayName("辐射通量(W)")]
        public double? RadiantFlux { get; set; }

        [DisplayName("光效(lm/W)")]
        public double? LuminousEfficacy { get; set; }

        public void CalculateEqe(float currentA)
        {
            if (currentA == 0)
            {
                Eqe = 0;
                OnPropertyChanged(nameof(Eqe));
                return;
            }

            const double h = 6.62607015e-34;
            const double c = 299792458.0;
            const double q = 1.602176634e-19;
            double step_nm = 1.0;
            if (fPL.Length > 2000)
            {
                step_nm = 0.1;
            }

            double sum_P_times_Lambda = 0.0;

            for (int i = 0; i < fPL.Length; i++)
            {
                double val = fPL[i];
                double lambda_nm = 380.0 + step_nm * i;
                sum_P_times_Lambda += val * lambda_nm;
            }

            double K_constant = (fPlambda * step_nm * 1.0e-9) / (h * c);

            double total_photons_per_sec = sum_P_times_Lambda * K_constant;
            double total_electrons_per_sec = currentA / q;

            Eqe = (total_photons_per_sec / total_electrons_per_sec);
            OnPropertyChanged(nameof(Eqe));
        }

        public void CalculateEqeParams(float voltage, float currentMA)
        {
            V = voltage;
            I = currentMA;

            if (currentMA != 0 && fPL != null && fPL.Length > 0)
            {
                CalculateEqe(currentMA / 1000f);
            }
            else
            {
                Eqe = 0;
            }

            LuminousFlux = fPh;

            if (fPL != null && fPL.Length > 0)
            {
                double step_nm = fPL.Length > 2000 ? 0.1 : 1.0;
                double sum_Power = 0.0;
                for (int i = 0; i < fPL.Length; i++)
                {
                    double P_val = fPlambda * fPL[i];
                    sum_Power += P_val;
                }
                RadiantFlux = sum_Power * step_nm;
            }

            if (V != 0 && I != 0)
            {
                double power_W = V * I / 1000.0;
                if (power_W != 0)
                {
                    LuminousEfficacy = fPh / power_W;
                }
            }

            OnPropertyChanged(nameof(Eqe));
            OnPropertyChanged(nameof(LuminousFlux));
            OnPropertyChanged(nameof(RadiantFlux));
            OnPropertyChanged(nameof(LuminousEfficacy));
        }

        public float V { get; set; }
        public float I { get; set; }
        /// <summary>
        /// IP
        /// </summary>
        [DisplayName("IP")]
        public string IP { get; set; }
        /// <summary>
        /// 亮度Lv(cd/m2)
        /// 
        [DisplayName("亮度Lv(cd/m2)")]
        public string Lv { get; set; }

        [DisplayName("CIEx")]
        public float fCIEx { get; set; }
        [DisplayName("CIEy")]
        public float fCIEy { get; set; }
        [DisplayName("CIEz")]
        public float fCIEz { get; set; }
        [DisplayName("2015CIEx")]
        public float fCIEx2015 { get; set; }
        [DisplayName("2015CIEy")]
        public float fCIEy2015 { get; set; }
        [DisplayName("2015CIEz")]
        public float fCIEz2015 { get; set; }


        [DisplayName("2015色度x")]
        public float fx2015 { get; set; }
        [DisplayName("2015色度y")]
        public float fy2015 { get; set; }
        [DisplayName("2015色度u")]
        public float fu2015 { get; set; }
        [DisplayName("2015色度v")]
        public float fv2015 { get; set; }

        /// <summary>
        /// 蓝光
        /// </summary>
        [DisplayName("蓝光")]
        public string Blue { get; set; }
        [DisplayName("色度x")]
        public float fx { get; set; }
        [DisplayName("色度y")]
        public float fy { get; set; }
        [DisplayName("色度u")]
        public float fu { get; set; }
        [DisplayName("色度v")]
        public float fv { get; set; }

        /// <summary>
        /// 相关色温(K)
        /// </summary>
        [DisplayName("相关色温(K)")]
        public float fCCT { get; set; }
        /// <summary>
        /// 色差dC
        /// </summary>
        public float dC { get; set; }
        /// <summary>
        /// 主波长(nm)
        /// </summary>
        [DisplayName("主波长Ld(nm)")]
        public float fLd { get; set; }
        /// <summary>
        /// 色纯度(%)
        /// </summary>
        [DisplayName("色纯度(%)")]
        public float fPur { get; set; }
        /// <summary>
        /// 峰值波长(nm)
        /// </summary>
        [DisplayName("峰值波长Lp(nm)")]
        public float fLp { get; set; }
        /// <summary>
        /// 半波宽(nm)
        /// </summary>
        [DisplayName("半波宽")]
        public float fHW { get; set; }
        /// <summary>
        /// 平均波长(nm)
        /// </summary>
        public float fLav { get; set; }
        /// <summary>
        /// 显色性指数 Ra
        /// </summary>
        [DisplayName("显色性指数Ra")]
        public float fRa { get; set; }
        /// <summary>
        /// 红色比
        /// </summary>
        public float fRR { get; set; }
        /// <summary>
        /// 绿色比
        /// </summary>
        public float fGR { get; set; }
        /// <summary>
        /// 蓝色比
        /// </summary>
        public float fBR { get; set; }
        /// <summary>
        /// 显色性指数 R1-R15
        /// </summary>
        public float[] fRi { get; set; }
        /// <summary>
        /// 峰值AD
        /// </summary>
        public float fIp { get; set; }
        /// <summary>
        /// 光度值
        /// </summary>
        public float fPh { get; set; }
        /// <summary>
        /// 辐射度值
        /// </summary>
        public float fPhe { get; set; }
        /// <summary>
        /// 绝对光谱洗漱
        /// </summary>
        public float fPlambda { get; set; }
        /// <summary>
        /// 起始波长
        /// </summary>
        public float fSpect1 { get; set; }
        /// <summary>
        /// 结束波长
        /// </summary>
        public float fSpect2 { get; set; }
        /// <summary>
        /// 波长间隔
        /// </summary>
        public float fInterval { get; set; }
        /// <summary>
        /// 光谱数据
        /// </summary>
        public float[] fPL { get; set; }

        /// <summary>
        /// 主波长对应颜色 (WPF Brush, click to copy hex)
        /// </summary>
        [JsonIgnore]
        [Browsable(false)]
        public WpfBrush DominantWavelengthColor { get; set; }

        /// <summary>
        /// 主波长对应颜色 Hex string
        /// </summary>
        [DisplayName("主波长颜色")]
        public string DominantWavelengthHex { get; set; }

        /// <summary>
        /// 兴奋纯度 (excitation purity), computed from CIE 1931 x,y coordinates.
        /// pe = sqrt((x - xn)^2 + (y - yn)^2) / sqrt((xd - xn)^2 + (yd - yn)^2)
        /// where (xn, yn) = D65 white point (0.3127, 0.3290)
        /// </summary>
        [DisplayName("兴奋纯度")]
        public double ExcitationPurity { get; set; }

        /// <summary>
        /// Computes excitation purity from CIE 1931 chromaticity coordinates.
        /// Uses the dominant wavelength direction on the CIE locus.
        /// </summary>
        public void ComputeExcitationPurity()
        {
            // D65 illuminant white point
            const double xn = 0.3127;
            const double yn = 0.3290;

            double dx = fx - xn;
            double dy = fy - yn;
            double distSample = Math.Sqrt(dx * dx + dy * dy);

            if (distSample < 1e-10)
            {
                ExcitationPurity = 0;
                return;
            }

            // Find the dominant wavelength point on the spectrum locus
            // by extending the line from white point through sample point
            // We approximate using the known spectrum locus boundary
            double angle = Math.Atan2(dy, dx);

            // Approximate dominant wavelength CIE coordinates on the locus
            // Use the fLd (dominant wavelength) if available
            double xd, yd;
            GetSpectrumLocusPoint(fLd, out xd, out yd);

            double dxd = xd - xn;
            double dyd = yd - yn;
            double distDominant = Math.Sqrt(dxd * dxd + dyd * dyd);

            if (distDominant < 1e-10)
            {
                ExcitationPurity = 0;
                return;
            }

            ExcitationPurity = Math.Round(distSample / distDominant, 4);
            if (ExcitationPurity > 1.0) ExcitationPurity = 1.0;
        }

        /// <summary>
        /// Approximate CIE 1931 chromaticity coordinates for a monochromatic wavelength
        /// on the spectrum locus. Uses a simplified piecewise linear approximation.
        /// </summary>
        private static void GetSpectrumLocusPoint(double wavelength, out double x, out double y)
        {
            // Simplified spectrum locus approximation (CIE 1931 2-degree observer)
            // Key wavelength -> (x, y) mapping
            double[][] locus = new double[][]
            {
                new[] { 380, 0.1741, 0.0050 },
                new[] { 400, 0.1733, 0.0048 },
                new[] { 420, 0.1714, 0.0051 },
                new[] { 440, 0.1644, 0.0109 },
                new[] { 460, 0.1440, 0.0297 },
                new[] { 470, 0.1241, 0.0578 },
                new[] { 480, 0.0913, 0.1327 },
                new[] { 490, 0.0454, 0.2950 },
                new[] { 500, 0.0082, 0.5384 },
                new[] { 510, 0.0139, 0.7502 },
                new[] { 520, 0.0743, 0.8338 },
                new[] { 530, 0.1547, 0.8059 },
                new[] { 540, 0.2296, 0.7543 },
                new[] { 550, 0.3016, 0.6923 },
                new[] { 560, 0.3731, 0.6245 },
                new[] { 570, 0.4441, 0.5547 },
                new[] { 580, 0.5125, 0.4866 },
                new[] { 590, 0.5752, 0.4242 },
                new[] { 600, 0.6270, 0.3725 },
                new[] { 610, 0.6658, 0.3340 },
                new[] { 620, 0.6915, 0.3083 },
                new[] { 630, 0.7079, 0.2920 },
                new[] { 640, 0.7190, 0.2809 },
                new[] { 650, 0.7260, 0.2740 },
                new[] { 660, 0.7300, 0.2700 },
                new[] { 680, 0.7320, 0.2680 },
                new[] { 700, 0.7347, 0.2653 },
                new[] { 780, 0.7347, 0.2653 },
            };

            // Clamp wavelength
            double wl = Math.Max(380, Math.Min(780, wavelength));

            // Find bounding entries
            for (int i = 0; i < locus.Length - 1; i++)
            {
                if (wl >= locus[i][0] && wl <= locus[i + 1][0])
                {
                    double t = (wl - locus[i][0]) / (locus[i + 1][0] - locus[i][0]);
                    x = locus[i][1] + t * (locus[i + 1][1] - locus[i][1]);
                    y = locus[i][2] + t * (locus[i + 1][2] - locus[i][2]);
                    return;
                }
            }

            // Fallback
            x = locus[locus.Length - 1][1];
            y = locus[locus.Length - 1][2];
        }

    }

}
