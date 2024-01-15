using System;
using System.Collections.ObjectModel;
using System.Drawing;
using ColorVision.MVVM;
using ColorVision.Services.Device.Spectrum.Dao;
using ColorVision.Sorts;
using Newtonsoft.Json;
using ScottPlot;
using ScottPlot.Plottable;
using static cvColorVision.GCSDLL;

namespace ColorVision.Device.Spectrum.Views
{

    public class ViewResultSpectrum : ViewModelBase,ISortable
    {
        private static int No;
        public int Id { get; set; }
        public DateTime? CreateTime { get; set; }
        public string? Batch { get; set; }
        public int? BatchID { get; set; }

        public ObservableCollection<SpectralData> SpectralDatas { get; set; } = new ObservableCollection<SpectralData>();

        public ScatterPlot ScatterPlot { get; set; }

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
            for (int i = 0; i <= 780; i += 10)
            {
                SpectralData SpectralData = new SpectralData();
                SpectralData.Wavelength = i / 10 + 380;
                SpectralData.RelativeSpectrum = fPL[i];
                SpectralData.AbsoluteSpectrum = fPL[i] * fPlambda;
                SpectralDatas.Add(SpectralData);
            }

            double[] x = new double[fPL.Length];
            double[] y = new double[fPL.Length];
            for (int i = 0; i < fPL.Length; i++)
            {
                x[i] = ((double)fSpect1 + Math.Round(fInterval, 1) * i);
                y[i] = fPL[i];
            }

            ScatterPlot = new ScatterPlot(x, y)
            {
                Color = Color.DarkGoldenrod,
                LineWidth = 1,
                MarkerSize = 1,
                Label = null,
                MarkerShape = MarkerShape.none,
                LineStyle = LineStyle.Solid
            };
        }

        public ViewResultSpectrum(SpectumResultModel item)
        {
            Id = item.Id;
            BatchID = item.BatchId;
            CreateTime = item.CreateDate;
            fx = item.fx ?? 0;
            fy = item.fy ?? 0;
            fu = item.fu ?? 0;
            fv = item.fv ?? 0;
            fCCT = item.fCCT ?? 0;
            dC = item.dC ?? 0;
            fLd = item.fLd ?? 0;
            fPur = item.fPur ?? 0;
            fLp = item.fLp ?? 0;
            fHW = item.fHW ?? 0;
            fLav = item.fLav ?? 0;
            fRa = item.fRa ?? 0;
            fRR = item.fRR ?? 0;
            fGR = item.fGR ?? 0;
            fBR = item.fBR ?? 0;
            fIp = item.fIp ?? 0;
            fPh = item.fPh ?? 0;
            fPhe = item.fPhe ?? 0;
            fPlambda = item.fPlambda ?? 0;
            fSpect1 = item.fSpect1 ?? 0;
            fSpect2 = item.fSpect2 ?? 0;
            fInterval = item.fInterval ?? 0;
            fPL = JsonConvert.DeserializeObject<float[]>(item.fPL ?? string.Empty) ?? System.Array.Empty<float>();
            fRi = JsonConvert.DeserializeObject<float[]>(item.fRi ?? string.Empty) ?? System.Array.Empty<float>();

            Gen();
        }


        public ViewResultSpectrum(ColorParam colorParam)
        {
            Id = No++; 
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
        public float V { get; set; }
        public float I { get; set; }

        /// <summary>
        /// IP
        /// </summary>
        public string IP { get; set; }
        /// <summary>
        /// 亮度Lv(cd/m2)
        /// </summary>
        public string Lv { get; set; }

        /// <summary>
        /// 蓝光
        /// </summary>
        public string Blue { get; set; }

        public float fx { get; set; }
        public float fy { get; set; }
        public float fu { get; set; }
        public float fv { get; set; }

        /// <summary>
        /// 相关色温(K)
        /// </summary>
        public float fCCT { get; set; }
        /// <summary>
        /// 色差dC
        /// </summary>
        public float dC { get; set; }
        /// <summary>
        /// 主波长(nm)
        /// </summary>
        public float fLd { get; set; }
        /// <summary>
        /// 色纯度(%)
        /// </summary>
        public float fPur { get; set; }
        /// <summary>
        /// 峰值波长(nm)
        /// </summary>
        public float fLp { get; set; }
        /// <summary>
        /// 半波宽(nm)
        /// </summary>
        public float fHW { get; set; }
        /// <summary>
        /// 平均波长(nm)
        /// </summary>
        public float fLav { get; set; }
        /// <summary>
        /// 显色性指数 Ra
        /// </summary>
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

    }

}
