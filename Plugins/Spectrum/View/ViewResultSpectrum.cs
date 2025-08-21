﻿using ColorVision.Common.MVVM;
using ColorVision.UI.Sorts;
using cvColorVision;
using Newtonsoft.Json;
using ScottPlot;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Spectrum
{

    public class ViewResultSpectrum : ViewModelBase,ISortID
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
            for (int i = 0; i < fPL.Length; i++)
            {
                xs[i] = ((double)fSpect1 + Math.Round(fInterval, 1) * i);
                ys[i] = fPL[i];
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

    }

}
