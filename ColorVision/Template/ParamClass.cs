using ColorVision.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Template
{

    public class ParamBase:ViewModelBase
    {
        public event EventHandler IsEnabledChanged;

        [Category("设置"), DisplayName("是否启用模板")]
        public bool IsEnable
        {
            get => _IsEnable; set
            {
                if (IsEnable == value) return;
                _IsEnable = value;
                if (value == true) IsEnabledChanged?.Invoke(this, new EventArgs());
                NotifyPropertyChanged();
            }
        }
        private bool _IsEnable;
    }


    /// <summary>
    /// 流程引擎模板
    /// </summary>
    public class FlowParam : ParamBase
    {
        /// <summary>
        /// 流程文件名称
        /// </summary>
        public string FileName { set; get; }

    }



    public class AoiParam: ParamBase
    {
        public bool FilterByArea { set; get; }
        public int MaxArea { set; get; }
        public int MinArea { set; get; }
        public bool FilterByContrast { set; get; }
        public float MaxContrast { set; get; }
        public float MinContrast { set; get; }
        public float ContrastBrightness { set; get; }
        public float ContrastDarkness { set; get; }
        public int BlurSize { set; get; }
        public int MinContourSize { set; get; }
        public int ErodeSize { set; get; }
        public int DilateSize { set; get; }
        [Category("left")]
        public int Left { set; get; }
        [Category("AoiRect")]
        public int Right { set; get; }
        [Category("AoiRect")]
        public int Top { set; get; }
        [Category("AoiRect")]
        public int Bottom { set; get; }
    };

    public class LedReusltParam : ParamBase
    {
        [Category("检测判断配置"), DefaultValue(1),DisplayName("灯珠抓取通道")]
        public int Channel { set; get; }
        [Category("检测判断配置"), DefaultValue(false)]
        public bool IsDebug { set; get; }
        [Category("检测判断配置"), DefaultValue(true), DisplayName("是否下限检测")]
        public bool IsLDetection { set; get; }
        [Category("检测判断配置"), DefaultValue(true), DisplayName("是否上限检测")]
        public bool IsHDetection { set; get; }

        [Category("参数X判断配置"), DefaultValue(false),DisplayName("X启用检测")]
        public bool IsXEnable { set; get; }
        [Category("参数X判断配置"), DefaultValue(0.2),DisplayName("X下限检测阈值百分比")]
        public float LLDetectionPX { set; get; }
        [Category("参数X判断配置"), DefaultValue(0),DisplayName("X下限检测阈值固定值")]
        public float LLDetectionFX { set; get; }
        [Category("参数X判断配置"), DefaultValue(1.8),DisplayName("X上限检测阈值百分比")]
        public float HLDetectionPX { set; get; }
        [Category("参数X判断配置"), DefaultValue(float.MaxValue), DisplayName("X上限检测阈值固定值")]
        public float HLDetectionFX { set; get; }

        [Category("参数Y判断配置"), DefaultValue(false),DisplayName("Y启用检测")]
        public bool IsYEnable { set; get; }
        [Category("参数Y判断配置"), DefaultValue(0.2), DisplayName("Y下限检测阈值百分比")]
        public float LLDetectionPY { set; get; }
        [Category("参数Y判断配置"), DefaultValue(0),DisplayName("Y下限检测阈值固定值")]
        public float LLDetectionFY { set; get; }
        [Category("参数Y判断配置"), DefaultValue(1.8), DisplayName("Y上限检测阈值百分比")]
        public float HLDetectionPY { set; get; }
        [Category("参数Y判断配置"), DefaultValue(float.MaxValue), DisplayName("Y上限检测阈值固定值")]
        public float HLDetectionFY { set; get; }

        [Category("参数Z判断配置"), DefaultValue(false),DisplayName("Z启用检测")]
        public bool IsZEnable { set; get; }
        [Category("参数Z判断配置"), DefaultValue(0.2), DisplayName("Z下限检测阈值百分比")]
        public float LLDetectionPZ { set; get; }
        [Category("参数Z判断配置"), DefaultValue(0), DisplayName("Z下限检测阈值固定值")]
        public float LLDetectionFZ { set; get; }
        [Category("参数Z判断配置"), DefaultValue(1.8), DisplayName("Z上限检测阈值百分比")]
        public float HLDetectionPZ { set; get; }
        [Category("参数Z判断配置"), DefaultValue(float.MaxValue), DisplayName("Z上限检测阈值固定值")]
        public float HLDetectionFZ { set; get; }

        [Category("参数x判断配置"), DefaultValue(false), DisplayName("x启用检测")]
        public bool IslxEnable { set; get; }
        [Category("参数x判断配置"), DefaultValue(0.2), DisplayName("x下限检测阈值百分比")]
        public float LLDetectionPlx { set; get; }
        [Category("参数x判断配置"), DefaultValue(0), DisplayName("x下限检测阈值固定值")]
        public float LLDetectionFlx { set; get; }
        [Category("参数x判断配置"), DefaultValue(1.8), DisplayName("x上限检测阈值百分比")]
        public float HLDetectionPlx { set; get; }
        [Category("参数x判断配置"), DefaultValue(float.MaxValue), DisplayName("x上限检测阈值固定值")]
        public float HLDetectionFlx { set; get; }

        [Category("参数y判断配置"), DefaultValue(false), DisplayName("y启用检测")]
        public bool IslyEnable { set; get; }
        [Category("参数y判断配置"), DefaultValue(0.2), DisplayName("y下限检测阈值百分比")]
        public float LLDetectionPly { set; get; }
        [Category("参数y判断配置"), DefaultValue(0), DisplayName("y下限检测阈值固定值")]
        public float LLDetectionFly { set; get; }
        [Category("参数y判断配置"), DefaultValue(1.8), DisplayName("y上限检测阈值百分比")]
        public float HLDetectionPly { set; get; }
        [Category("参数y判断配置"), DefaultValue(float.MaxValue), DisplayName("y上限检测阈值固定值")]
        public float HLDetectionFly { set; get; }

        [Category("参数u判断配置"), DefaultValue(false), DisplayName("u启用检测")]
        public bool IsluEnable { set; get; }
        [Category("参数u判断配置"), DefaultValue(0.2), DisplayName("u下限检测阈值百分比")]
        public float LLDetectionPlu { set; get; }
        [Category("参数u判断配置"), DefaultValue(0), DisplayName("u下限检测阈值固定值")]
        public float LLDetectionFlu { set; get; }
        [Category("参数u判断配置"), DefaultValue(1.8), DisplayName("u上限检测阈值百分比")]
        public float HLDetectionPlu { set; get; }
        [Category("参数u判断配置"), DefaultValue(float.MaxValue), DisplayName("u上限检测阈值固定值")]
        public float HLDetectionFlu { set; get; }

        [Category("参数v判断配置"), DefaultValue(false), DisplayName("v启用检测")]
        public bool IslvEnable { set; get; }
        [Category("参数v判断配置"), DefaultValue(0.2), DisplayName("v下限检测阈值百分比")]
        public float LLDetectionPlv { set; get; }
        [Category("参数v判断配置"), DefaultValue(0), DisplayName("v下限检测阈值固定值")]
        public float LLDetectionFlv { set; get; }
        [Category("参数v判断配置"), DefaultValue(1.8), DisplayName("v上限检测阈值百分比")]
        public float HLDetectionPlv { set; get; }
        [Category("参数v判断配置"), DefaultValue(float.MaxValue), DisplayName("v上限检测阈值固定值")]
        public float HLDetectionFlv { set; get; }

        [Category("参数CCT判断配置"), DefaultValue(false), DisplayName("CCT启用检测")]
        public bool IsCCTEnable { set; get; }
        [Category("参数CCT判断配置"), DefaultValue(0.2), DisplayName("CCT下限检测阈值百分比")]
        public float LLDetectionPCCT { set; get; }
        [Category("参数CCT判断配置"), DefaultValue(0), DisplayName("CCT下限检测阈值固定值")]
        public float LLDetectionFCCT { set; get; }
        [Category("参数CCT判断配置"), DefaultValue(1.8), DisplayName("CCT上限检测阈值百分比")]
        public float HLDetectionPCCT { set; get; }
        [Category("参数CCT判断配置"), DefaultValue(float.MaxValue), DisplayName("CCT上限检测阈值固定值")]
        public float HLDetectionFCCT { set; get; }

        [Category("参数DW判断配置"), DefaultValue(false), DisplayName("DW启用检测")]
        public bool IsDWTEnable { set; get; }
        [Category("参数DW判断配置"), DefaultValue(0.2), DisplayName("DW下限检测阈值百分比")]
        public float LLDetectionPDW { set; get; }
        [Category("参数DW判断配置"), DefaultValue(0), DisplayName("DW下限检测阈值固定值")]
        public float LLDetectionFDW { set; get; }
        [Category("参数DW判断配置"), DefaultValue(1.8), DisplayName("DW上限检测阈值百分比")]
        public float HLDetectionPDW { set; get; }
        [Category("参数DW判断配置"), DefaultValue(float.MaxValue), DisplayName("DW上限检测阈值固定值")]
        public float HLDetectionFDW { set; get; }

        [Category("参数亮度均匀性判断配置"), DefaultValue(false), DisplayName("亮度均匀性启用检测")]
        public bool IsBUEnable { set; get; }
        [Category("参数亮度均匀性判断配置"), DefaultValue(0.2), DisplayName("亮度均匀性下限检测阈值百分比")]
        public float LLDetectionPBU { set; get; }
        [Category("参数亮度均匀性判断配置"), DefaultValue(0), DisplayName("亮度均匀性下限检测阈值固定值")]
        public float LLDetectionFBU { set; get; }
        [Category("参数亮度均匀性判断配置"), DefaultValue(1.8), DisplayName("亮度均匀性上限检测阈值百分比")]
        public float HLDetectionPBU { set; get; }
        [Category("参数亮度均匀性判断配置"), DefaultValue(float.MaxValue), DisplayName("亮度均匀性上限检测阈值固定值")]
        public float HLDetectionFBU { set; get; }

        [Category("参数色度均匀性判断配置"), DefaultValue(false), DisplayName("色度均匀性启用检测")]
        public bool IsCUEnable { set; get; }
        [Category("参数色度均匀性判断配置"), DefaultValue(0.2), DisplayName("色度均匀性下限检测阈值百分比")]
        public float LLDetectionPCU { set; get; }
        [Category("参数色度均匀性判断配置"), DefaultValue(0), DisplayName("色度均匀性下限检测阈值固定值")]
        public float LLDetectionFCU { set; get; }
        [Category("参数色度均匀性判断配置"), DefaultValue(1.8), DisplayName("色度均匀性上限检测阈值百分比")]
        public float HLDetectionPCU { set; get; }
        [Category("参数色度均匀性判断配置"), DefaultValue(float.MaxValue), DisplayName("色度均匀性上限检测阈值固定值")]
        public float HLDetectionFCU { set; get; }

        [Category("检测判断配置"), DisplayName("判断方式"), Description("1-仅通过百分比阈值判断\r\n2-仅通过固定阈值判断\r\n3-结合两种方式同时判断") ]
        public int JudgingWay { set; get; }

        [Category("检测判断配置"), DisplayName("多参数判断方式"), Description("1-选定参数中有一个判断NG即返回NG\r\n2-当所有选定参数都判定NG时返回NG")  ]
        public int JudgingWayMul { set; get; }
    }


}
