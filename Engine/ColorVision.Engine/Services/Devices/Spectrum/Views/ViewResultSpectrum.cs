using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using cvColorVision;
using iText.Commons.Bouncycastle.Asn1.X509;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using ScottPlot;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Spectrum.Views
{
    public class cieData
    {
        public double fCIEx { get; set; }
        public double fCIEy { get; set; }
        public double fCIEz { get; set; }
        public double fu_2015 { get; set; }
        public double fv_2015 { get; set; }
        public double fx_2015 { get; set; }
        public double fy_2015 { get; set; }
        public double fCIEx_2015 { get; set; }
        public double fCIEy_2015 { get; set; }
        public double fCIEz_2015 { get; set; }
    }


    public class ViewResultSpectrum : ViewModelBase
    {
        private static int No = 1;
        public ContextMenu ContextMenu { get; set; }

        [DisplayName("SerialNumber1")]
        public int Id { get; set; }

        [DisplayName("CreateTime")]
        public DateTime? CreateTime { get; set; } = DateTime.Now;
        public string? Batch { get; set; }
        public int? BatchID { get; set; }

        public ObservableCollection<SpectralData> SpectralDatas { get; set; } = new ObservableCollection<SpectralData>();

        public Scatter ScatterPlot { get; set; }
        public Scatter AbsoluteScatterPlot { get; set; }

        // ==========================================
        // 1. Added New Properties
        // ==========================================

        /// <summary>
        /// External Quantum Efficiency (%)
        /// Requires Current (I) to calculate.
        /// </summary>
        [DisplayName("EQE (%)")]
        public double? Eqe { get; set; }

        /// <summary>
        /// Luminous Flux (lm) - Often mapped from fPh
        /// </summary>
        [DisplayName("Luminous Flux (lm)")]
        public float? LuminousFlux { get; set; }

        /// <summary>
        /// Radiant Flux (W) - Often mapped from fPhe
        /// </summary>
        [DisplayName("Radiant Flux (W)")]
        public double? RadiantFlux { get; set; }

        /// <summary>
        /// Luminous Efficacy (lm/W)
        /// </summary>
        [DisplayName("Luminous Efficacy (lm/W)")]
        public double? LuminousEfficacy { get; set; }

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
                int length = fPL.Length>4000 ?4000:fPL.Length;
                double[] xs = new double[length];
                double[] ys = new double[length];
                double[] ysAbsolute = new double[length];
                for (int i = 0; i < length; i++)
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


        // ==========================================
        // 2. Logic to Calculate EQE
        // ==========================================
        /// <summary>
        /// Recalculates EQE based on the provided Current (Amps)
        /// Call this method from your Right-Click Menu Command.
        /// </summary>
        /// <param name="currentA">Current in Amps</param>
        public void CalculateEqe(float currentA)
        {
            if (currentA == 0)
            {
                Eqe = 0;
                return;
            }

            // Constants
            const double h = 6.62607015e-34;
            const double c = 299792458.0;
            const double q = 1.602176634e-19; // Elementary charge
            double step_nm = 1.0;
            if (fPL.Length > 2000)
            {
                step_nm = 0.1;
            }

            double sum_P_times_Lambda = 0.0;

            // 使用 fPL.Length 防止数组越界，不要写死 4001
            for (int i = 0; i < fPL.Length; i++)
            {
                double val = fPL[i];

                // 当前波长
                double lambda_nm = 380.0 + step_nm * i;

                // 积分累加项：归一化光谱 * 波长
                sum_P_times_Lambda += val * lambda_nm;
            }

            // 提取公共常数计算
            // 公式推导: TotalPhotons = Sum(P * step * lambda_m / hc)
            // P = val * fPlambda
            // lambda_m = lambda_nm * 1e-9
            // 组合: (val * fPlambda) * step_nm * (lambda_nm * 1e-9) / (hc)

            double divisor = ViewSpectrumConfig.Instance.divisor;

            // 【修复 2】: 将 divisor 应用到 fPlambda
            // 公式: TotalPhotons = Sum(P * step * lambda_m / hc)
            // 系数 K = (fPlambda * divisor * step_nm * 1.0e-9) / (h * c)
            double K_constant = (fPlambda * divisor * step_nm * 1.0e-9) / (h * c);

            double total_photons_per_sec = sum_P_times_Lambda * K_constant;
            double total_electrons_per_sec = currentA / q;

            if (total_electrons_per_sec != 0)
            {
                // 【注意单位】: 
                // 如果您希望和 C++ 结果完全一致（比率），请去掉 * 100.0
                // 如果您希望显示百分比（通常 UI 显示都是百分比），请保留 * 100.0，但需知悉 C++ 算出的是比率。
                Eqe = (total_photons_per_sec / total_electrons_per_sec);
            }
            else
            {
                Eqe = 0;
            }

            OnPropertyChanged(nameof(Eqe));
        }

        public float? IntTime { get; set; }

        public ViewResultSpectrum()
        {

        }

        public ViewResultSpectrum(SpectumResultEntity item)
        {
            Id = item.Id;
            BatchID = item.BatchId;
            CreateTime = item.CreateDate;
            V = item.VResult;
            I = item.IResult;
            IntTime = item.IntTime;
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


            // EQE-specific fields
            Eqe = item.Eqe ?? 0;
            LuminousFlux = item.LuminousFlux ?? 0;
            RadiantFlux = item.RadiantFlux ?? 0;
            LuminousEfficacy = item.LuminousEfficacy ?? 0;



            if (!string.IsNullOrWhiteSpace(item.fPL_file_name) && File.Exists(item.fPL_file_name))
            {
                File.ReadAllBytes(item.fPL_file_name);
                string json = File.ReadAllText(item.fPL_file_name);
                fPL = JsonConvert.DeserializeObject<float[]>(json) ?? Array.Empty<float>();
            }
            else
            {
                fPL = JsonConvert.DeserializeObject<float[]>(item.fPL ?? string.Empty) ?? Array.Empty<float>();
            }



            fRi = JsonConvert.DeserializeObject<float[]>(item.fRi ?? string.Empty) ?? Array.Empty<float>();

            cieData cieData = JsonConvert.DeserializeObject<cieData>(item.CieDataEx ?? string.Empty)?? new cieData();


            fCIEx = cieData.fCIEx;
            fCIEy = cieData.fCIEy;
            fCIEz = cieData.fCIEz;
            fu_2015 = cieData.fu_2015;
            fv_2015 = cieData.fv_2015;
            fx_2015 = cieData.fx_2015;
            fy_2015 = cieData.fy_2015;
            fCIEx_2015 = cieData.fCIEx_2015;
            fCIEy_2015 = cieData.fCIEy_2015;
            fCIEz_2015 = cieData.fCIEz_2015;

            LuminousFlux = (float)(fCIEy * ViewSpectrumConfig.Instance.divisor);

            //if (fPL.Length > 0)
            //{
            //    double step_nm = 0.1;
            //    double sum_Power = 0.0;

            //    for (int i = 0; i < 4001; i++)
            //    {
            //        double P_val = fPlambda * fPL[i]; // 绝对功率 (W/nm)
            //        sum_Power += P_val;
            //    }
            //    RadiantFlux = sum_Power * step_nm;
            //}

            //if (item.SmuDataId > 0)
            //{
            //    var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
            //    var smuResult = DB.Queryable<SMUResultModel>().Where(x => x.Id == item.SmuDataId).First();
            //    DB.Dispose();
            //    if (smuResult != null)
            //    {
            //        V = smuResult.VResult;
            //        I = smuResult.IResult;
            //    }

            //    if (I.HasValue && I.Value != 0 && fPL.Length > 0)
            //    {
            //        CalculateEqe(I.Value /1000);
            //    }

            //    if (RadiantFlux.HasValue && RadiantFlux.Value != 0)
            //    {
            //        LuminousEfficacy = LuminousFlux / (V*I/1000);
            //    }
            //    else
            //    {
            //        LuminousEfficacy = 0;
            //    }
            //}

            Gen();

            RelayCommand relayCommand = new RelayCommand(a => CalculateEqe());
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header ="计算EQE",Command = relayCommand });
        }

        private void CalculateEqe()
        {
            string input = Microsoft.VisualBasic.Interaction.InputBox("请输入电流 (mA):", "计算 EQE", "1");
            if (float.TryParse(input, out float currentA))
            {
                // 更新 ViewModel 中的电流属性（可选）
                I = currentA;
                // 调用我们在 ViewModel 中新加的方法
                CalculateEqe(currentA);
            }
            else
            {
                MessageBox.Show("请输入有效的电流数值。");
                return;
            }
            string input1 = Microsoft.VisualBasic.Interaction.InputBox("请输入电压 (V):", "光效", "5");
            if (float.TryParse(input1, out float v))
            {
                V=v;
                if (RadiantFlux.HasValue && RadiantFlux.Value != 0)
                {
                    LuminousEfficacy = LuminousFlux / (V * I / 1000);
                }
                else
                {
                    LuminousEfficacy = 0;
                }
            }
            else
            {
                MessageBox.Show("请输入有效的电压数值。");
            }


        }


        public ViewResultSpectrum(COLOR_PARA item)
        {
            Id = No++; 
            fx = item.fx;
            fy = item.fy;
            fu = item.fu;
            fv = item.fv;
            fCCT = item.fCCT;
            dC = item.dC;
            fLd = item.fLd;
            fPur = item.fPur;
            fLp = item.fLp;
            fHW = item.fHW;
            fLav = item.fLav;
            dC = item.dC;
            fRa = item.fRa;
            fRR = item.fRR;
            fGR = item.fGR;
            fBR = item.fBR;
            fRi = item.fRi;
            fIp = item.fIp;
            fPh = item.fPh;
            fPhe = item.fPhe;
            fPlambda = item.fPlambda;
            fSpect1 = item.fSpect1;
            fSpect2 = item.fSpect2;
            fInterval = item.fInterval;
            fPL = item.fPL;
            fCIEx  = item.fCIEx;
            fCIEy  = item.fCIEy;
            fCIEz  = item.fCIEz;
            fu_2015  = item.fu_2015;
            fv_2015  = item.fv_2015;
            fx_2015  = item.fx_2015;
            fy_2015 = item.fy_2015;
            fCIEx_2015  = item.fCIEx_2015;
            fCIEy_2015 = item.fCIEy_2015;
            fCIEz_2015 = item.fCIEz_2015;


            //LuminousFlux = (float)(fCIEy * ViewSpectrumConfig.Instance.divisor);

            //if (fPL.Length > 0)
            //{
            //    double step_nm = 0.1;
            //    double sum_Power = 0.0;

            //    for (int i = 0; i < 4001; i++)
            //    {
            //        double P_val = fPlambda * fPL[i]; // 绝对功率 (W/nm)
            //        sum_Power += P_val;
            //    }
            //    RadiantFlux = sum_Power * step_nm;
            //}

            Gen();
        }

        public double fCIEx { get; set; }
        public double fCIEy { get; set; }
        public double fCIEz { get; set; }
        public double fu_2015 { get; set; }
        public double fv_2015 { get; set; }
        public double fx_2015 { get; set; }
        public double fy_2015 { get; set; }
        public double fCIEx_2015 { get; set; }
        public double fCIEy_2015 { get; set; }
        public double fCIEz_2015 { get; set; }

        public float? V { get; set; }
        public float? I { get => _I; set { _I = value; OnPropertyChanged(); } }
        private float? _I;

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
