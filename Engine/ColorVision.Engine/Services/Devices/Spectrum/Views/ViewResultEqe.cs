using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using Newtonsoft.Json;
using ScottPlot;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Spectrum.Views
{
    public class ViewResultEqe : ViewModelBase
    {
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
                int length = fPL.Length > 4000 ? 4000 : fPL.Length;
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
            catch (Exception ex)
            {
                // Silently handle exceptions during generation
            }
        }

        public float? IntTime { get; set; }

        public ViewResultEqe()
        {
        }


        public ViewResultEqe(SpectumResultEntity item)
        {
            Id = item.Id;
            BatchID = item.BatchId;
            CreateTime = item.CreateDate;
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

            V = item.VResult;
            I = item.IResult;

            // EQE-specific fields
            Eqe = item.Eqe ?? 0;
            LuminousFlux = item.LuminousFlux ?? 0;
            RadiantFlux = item.RadiantFlux ?? 0;
            LuminousEfficacy = item.LuminousEfficacy ?? 0;

            if (!string.IsNullOrWhiteSpace(item.fPL_file_name) && File.Exists(item.fPL_file_name))
            {
                string json = File.ReadAllText(item.fPL_file_name);
                fPL = JsonConvert.DeserializeObject<float[]>(json) ?? Array.Empty<float>();
            }
            else
            {
                fPL = JsonConvert.DeserializeObject<float[]>(item.fPL ?? string.Empty) ?? Array.Empty<float>();
            }

            Gen();
            if (item.SmuDataId > 0)
            {
                var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

                // Query SMU results if available
                var smuResult = DB.Queryable<SMUResultModel>().Where(x => x.Id == item.SmuDataId).First();
                DB.Dispose();
                if (smuResult != null)
                {
                    V = smuResult.VResult;
                    I = smuResult.IResult;
                }
            }
            VerifyRadiantFluxCommand = new RelayCommand(a => VerifyRadiantFlux());

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem
            {
                Header = "Verify Radiant Flux",
                Command = VerifyRadiantFluxCommand
            });
        }

        public RelayCommand VerifyRadiantFluxCommand { get; set; }

        // ==========================================
        // 2. 验算逻辑方法
        // ==========================================
        /// <summary>
        /// 验算/重新计算辐射通量 (W)
        /// 此逻辑与 C++ 中的 dW 积分逻辑完全一致
        /// </summary>
        public void VerifyRadiantFlux()
        {
            if (fPL == null || fPL.Length == 0) return;

            // 1. 确定步长
            // 4001个点 (380-780) -> 0.1nm
            // 401个点 (380-780) -> 1.0nm
            double step_nm = 1.0;
            if (fPL.Length > 401) step_nm = 0.1;

            double sum_Power = 0.0;

            // 2. 积分 Sum(P_lambda * step)
            // P_lambda(W/nm) = 相对光谱值(fPL) * 绝对系数(fPlambda)
            for (int i = 0; i < fPL.Length; i++)
            {
                sum_Power += fPL[i] * fPlambda;
            }

            // 3. 更新辐射通量 (W)
            // 公式: dW = Sum(P) * dl
            RadiantFlux = sum_Power * step_nm;

            // 4. 同步更新光效 (lm/W)
            // 光效 = 光通量(lm) / 辐射通量(W)
            // 优先使用 fPh (C++中的 dIm / Luminous Flux)
            double flux_lm = fPh;
            if (LuminousFlux != 0) flux_lm = LuminousFlux;

            if (RadiantFlux != 0)
            {
                LuminousEfficacy = flux_lm / RadiantFlux;
            }
            else
            {
                LuminousEfficacy = 0;
            }
        }

        public float? V { get; set; }
        public float? I { get; set; }

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
        /// 绝对光谱系数
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

        // EQE-specific properties
        /// <summary>
        /// EQE
        /// </summary>
        public double Eqe { get; set; }

        /// <summary>
        /// 光通量 (lm)
        /// </summary>
        public float LuminousFlux { get; set; }

        /// <summary>
        /// 辐射通量 (W)
        /// </summary>
        public double RadiantFlux { get; set; }

        /// <summary>
        /// 光效 (lm/W)
        /// </summary>
        public double LuminousEfficacy { get; set; }
    }
}
