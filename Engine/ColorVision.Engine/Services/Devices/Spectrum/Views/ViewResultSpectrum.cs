using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using cvColorVision;
using Newtonsoft.Json;
using ScottPlot;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;

namespace ColorVision.Engine.Services.Devices.Spectrum.Views
{



    public class ViewResultSpectrum : ViewModelBase
    {
        private static int No;

        [DisplayName("SerialNumber1")]
        public int Id { get; set; }

        [DisplayName("CreateTime")]
        public DateTime? CreateTime { get; set; } = DateTime.Now;
        public string? Batch { get; set; }
        public int? BatchID { get; set; }

        public ObservableCollection<SpectralData> SpectralDatas { get; set; } = new ObservableCollection<SpectralData>();

        public Scatter ScatterPlot { get; set; }
        public Scatter AbsoluteScatterPlot { get; set; }

        public void Gen()
        {
            try
            {
                IP = Math.Round(fIp / 65535 * 100, 2).ToString() + "%";
                Lv = (fPh / 1).ToString();

                double sum1 = 0, sum2 = 0;
                for (int i = 35; i <= 75; i++)
                    sum1 += fPL[i * 10];
                for (int i = 20; i <= 120; i++)
                    sum2 += fPL[i * 10];
                Blue = Math.Round(sum1 / sum2 * 100, 2).ToString();
                for (int i = 0; i <= (780 - 380) * 10; i += 10)
                {
                    SpectralData SpectralData = new();
                    SpectralData.Wavelength = i / 10 + 380;
                    SpectralData.RelativeSpectrum = fPL[i] > 0 ? fPL[i] : 0;
                    SpectralData.AbsoluteSpectrum = fPL[i] * fPlambda;
                    SpectralDatas.Add(SpectralData);
                }

                fSpect1 = 380;
                fSpect2 = 780;
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

            }
            catch(Exception ex)
            {
                
            }

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
            fPL = JsonConvert.DeserializeObject<float[]>(item.fPL ?? string.Empty) ?? Array.Empty<float>();
            fRi = JsonConvert.DeserializeObject<float[]>(item.fRi ?? string.Empty) ?? Array.Empty<float>();


            Gen();
        }


        public ViewResultSpectrum(COLOR_PARA colorParam)
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
