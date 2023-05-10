#pragma warning disable CA1708,CA1507
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

        [JsonProperty("enable")]
        [Category("_Setting"), DisplayName("是否启用")]
        public bool IsEnable { get => _IsEnable; set {
                if (IsEnable == value) return;
                _IsEnable = value; 
                if (value == true) IsEnabledChanged?.Invoke(this, new EventArgs()); 
                NotifyPropertyChanged(); } 
        }
        private bool _IsEnable;
    }

    public class AoiParam: ParamBase
    {
        [JsonProperty("filter_by_area")]
        public bool FilterByArea { set; get; }
        [JsonProperty("max_area")]
        public int MaxArea { set; get; }
        [JsonProperty("min_area")]
        public int MinArea { set; get; }
        [JsonProperty("filter_by_contrast")]
        public bool FilterByContrast { set; get; }
        [JsonProperty("max_contrast")]
        public float MaxContrast { set; get; }
        [JsonProperty("min_contrast")]
        public float MinContrast { set; get; }
        [JsonProperty("contrast_brightness")]
        public float ContrastBrightness { set; get; }
        [JsonProperty("contrast_darkness")]
        public float ContrastDarkness { set; get; }
        [JsonProperty("blur_size")]
        public int BlurSize { set; get; }
        [JsonProperty("min_contour_size")]
        public int MinContourSize { set; get; }
        [JsonProperty("erode_size")]
        public int ErodeSize { set; get; }
        [JsonProperty("dilate_size")]
        public int DilateSize { set; get; }
        [JsonProperty("contrast_brightness")]
        [Category("left")]
        public int Left { set; get; }
        [Category("AoiRect")]
        [JsonProperty("contrast_brightness")]
        public int Right { set; get; }
        [Category("AoiRect")]
        [JsonProperty("top")]
        public int Top { set; get; }
        [Category("AoiRect")]
        [JsonProperty("bottom")]
        public int Bottom { set; get; }
    };

    public class LedReusltParam : ParamBase
    {
        [Category("检测判断配置"), DefaultValue(1),DisplayName("灯珠抓取通道")]
        [JsonProperty("灯珠抓取通道")]
        public int Channel { set; get; }
        [Category("检测判断配置"), DefaultValue(false)]
        [JsonProperty("isdebug")]
        public bool IsDebug { set; get; }
        [Category("检测判断配置"), DefaultValue(true), DisplayName("是否下限检测")]
        [JsonProperty("是否下限检测")]
        public bool IsLDetection { set; get; }
        [Category("检测判断配置"), DefaultValue(true), DisplayName("是否上限检测")]
        [JsonProperty("是否上限检测")]
        public bool IsHDetection { set; get; }

        [Category("参数X判断配置"), DefaultValue(false),DisplayName("X启用检测")]
        [JsonProperty("X启用检测")]
        public bool IsXEnable { set; get; }
        [Category("参数X判断配置"), DefaultValue(0.2),DisplayName("X下限检测阈值百分比")]
        [JsonProperty("X下限检测阈值百分比")]
        public float LLDetectionPX { set; get; }
        [Category("参数X判断配置"), DefaultValue(0),DisplayName("X下限检测阈值固定值")]
        [JsonProperty("X下限检测阈值固定值")]
        public float LLDetectionFX { set; get; }
        [Category("参数X判断配置"), DefaultValue(1.8),DisplayName("X上限检测阈值百分比")]
        [JsonProperty("X上限检测阈值百分比")]
        public float HLDetectionPX { set; get; }
        [Category("参数X判断配置"), DefaultValue(float.MaxValue), DisplayName("X上限检测阈值固定值")]
        [JsonProperty("X上限检测阈值固定值")]
        public float HLDetectionFX { set; get; }

        [Category("参数Y判断配置"), DefaultValue(false),DisplayName("Y启用检测")]
        [JsonProperty("Y启用检测")]
        public bool IsYEnable { set; get; }
        [Category("参数Y判断配置"), DefaultValue(0.2), DisplayName("Y下限检测阈值百分比")]
        [JsonProperty("Y下限检测阈值百分比")]
        public float LLDetectionPY { set; get; }
        [Category("参数Y判断配置"), DefaultValue(0),DisplayName("Y下限检测阈值固定值")]
        [JsonProperty("Y下限检测阈值固定值")]
        public float LLDetectionFY { set; get; }
        [Category("参数Y判断配置"), DefaultValue(1.8), DisplayName("Y上限检测阈值百分比")]
        [JsonProperty("Y上限检测阈值百分比")]
        public float HLDetectionPY { set; get; }
        [Category("参数Y判断配置"), DefaultValue(float.MaxValue), DisplayName("Y上限检测阈值固定值")]
        [JsonProperty("Y上限检测阈值固定值")]
        public float HLDetectionFY { set; get; }

        [Category("参数Z判断配置"), DefaultValue(false),DisplayName("Z启用检测")]
        [JsonProperty("Z启用检测")]
        public bool IsZEnable { set; get; }
        [Category("参数Z判断配置"), DefaultValue(0.2), DisplayName("Z下限检测阈值百分比")]
        [JsonProperty("Z下限检测阈值百分比")]
        public float LLDetectionPZ { set; get; }
        [Category("参数Z判断配置"), DefaultValue(0), DisplayName("Z下限检测阈值固定值")]
        [JsonProperty("Z下限检测阈值固定值")]
        public float LLDetectionFZ { set; get; }
        [Category("参数Z判断配置"), DefaultValue(1.8), DisplayName("Z上限检测阈值百分比")]
        [JsonProperty("Z上限检测阈值百分比")]
        public float HLDetectionPZ { set; get; }
        [Category("参数Z判断配置"), DefaultValue(float.MaxValue), DisplayName("Z上限检测阈值固定值")]
        [JsonProperty("Z上限检测阈值固定值")]
        public float HLDetectionFZ { set; get; }

        [Category("参数x判断配置"), DefaultValue(false), DisplayName("x启用检测")]
        [JsonProperty("x启用检测")]
        public bool IszEnable { set; get; }
        [Category("参数x判断配置"), DefaultValue(0.2), DisplayName("x下限检测阈值百分比")]
        [JsonProperty("x下限检测阈值百分比")]
        public float LLDetectionPx { set; get; }
        [Category("参数x判断配置"), DefaultValue(0), DisplayName("x下限检测阈值固定值")]
        [JsonProperty("x下限检测阈值固定值")]
        public float LLDetectionFx { set; get; }
        [Category("参数x判断配置"), DefaultValue(1.8), DisplayName("x上限检测阈值百分比")]
        [JsonProperty("x上限检测阈值百分比")]
        public float HLDetectionPx { set; get; }
        [Category("参数x判断配置"), DefaultValue(float.MaxValue), DisplayName("x上限检测阈值固定值")]
        [JsonProperty("x上限检测阈值固定值")]
        public float HLDetectionFx { set; get; }

        [Category("参数y判断配置"), DefaultValue(false), DisplayName("y启用检测")]
        [JsonProperty("y启用检测")]
        public bool IsyEnable { set; get; }
        [Category("参数y判断配置"), DefaultValue(0.2), DisplayName("y下限检测阈值百分比")]
        [JsonProperty("y下限检测阈值百分比")]
        public float LLDetectionPy { set; get; }
        [Category("参数y判断配置"), DefaultValue(0), DisplayName("y下限检测阈值固定值")]
        [JsonProperty("y下限检测阈值固定值")]
        public float LLDetectionFy { set; get; }
        [Category("参数y判断配置"), DefaultValue(1.8), DisplayName("y上限检测阈值百分比")]
        [JsonProperty("y上限检测阈值百分比")]
        public float HLDetectionPy { set; get; }
        [Category("参数y判断配置"), DefaultValue(float.MaxValue), DisplayName("y上限检测阈值固定值")]
        [JsonProperty("y上限检测阈值固定值")]
        public float HLDetectionFy { set; get; }

        [Category("参数u判断配置"), DefaultValue(false), DisplayName("u启用检测")]
        [JsonProperty("u启用检测")]
        public bool IsuEnable { set; get; }
        [Category("参数u判断配置"), DefaultValue(0.2), DisplayName("u下限检测阈值百分比")]
        [JsonProperty("u下限检测阈值百分比")]
        public float LLDetectionPu { set; get; }
        [Category("参数u判断配置"), DefaultValue(0), DisplayName("u下限检测阈值固定值")]
        [JsonProperty("u下限检测阈值固定值")]
        public float LLDetectionFu { set; get; }
        [Category("参数u判断配置"), DefaultValue(1.8), DisplayName("u上限检测阈值百分比")]
        [JsonProperty("u上限检测阈值百分比")]
        public float HLDetectionPu { set; get; }
        [Category("参数u判断配置"), DefaultValue(float.MaxValue), DisplayName("u上限检测阈值固定值")]
        [JsonProperty("u上限检测阈值固定值")]
        public float HLDetectionFu { set; get; }

        [Category("参数v判断配置"), DefaultValue(false), DisplayName("v启用检测")]
        [JsonProperty("v启用检测")]
        public bool IsvEnable { set; get; }
        [Category("参数v判断配置"), DefaultValue(0.2), DisplayName("v下限检测阈值百分比")]
        [JsonProperty("v下限检测阈值百分比")]
        public float LLDetectionPv { set; get; }
        [Category("参数v判断配置"), DefaultValue(0), DisplayName("v下限检测阈值固定值")]
        [JsonProperty("v下限检测阈值固定值")]
        public float LLDetectionFv { set; get; }
        [Category("参数v判断配置"), DefaultValue(1.8), DisplayName("v上限检测阈值百分比")]
        [JsonProperty("v上限检测阈值百分比")]
        public float HLDetectionPv { set; get; }
        [Category("参数v判断配置"), DefaultValue(float.MaxValue), DisplayName("v上限检测阈值固定值")]
        [JsonProperty("v上限检测阈值固定值")]
        public float HLDetectionFv { set; get; }

        [Category("参数CCT判断配置"), DefaultValue(false), DisplayName("CCT启用检测")]
        [JsonProperty("CCT启用检测")]
        public bool IsCCTEnable { set; get; }
        [Category("参数CCT判断配置"), DefaultValue(0.2), DisplayName("CCT下限检测阈值百分比")]
        [JsonProperty("CCT下限检测阈值百分比")]
        public float LLDetectionPCCT { set; get; }
        [Category("参数CCT判断配置"), DefaultValue(0), DisplayName("CCT下限检测阈值固定值")]
        [JsonProperty("CCT下限检测阈值固定值")]
        public float LLDetectionFCCT { set; get; }
        [Category("参数CCT判断配置"), DefaultValue(1.8), DisplayName("CCT上限检测阈值百分比")]
        [JsonProperty("CCT上限检测阈值百分比")]
        public float HLDetectionPCCT { set; get; }
        [Category("参数CCT判断配置"), DefaultValue(float.MaxValue), DisplayName("CCT上限检测阈值固定值")]
        [JsonProperty("CCT上限检测阈值固定值")]
        public float HLDetectionFCCT { set; get; }

        [Category("参数DW判断配置"), DefaultValue(false), DisplayName("DW启用检测")]
        [JsonProperty("DW启用检测")]
        public bool IsDWTEnable { set; get; }
        [Category("参数DW判断配置"), DefaultValue(0.2), DisplayName("DW下限检测阈值百分比")]
        [JsonProperty("DW下限检测阈值百分比")]
        public float LLDetectionPDW { set; get; }
        [Category("参数DW判断配置"), DefaultValue(0), DisplayName("DW下限检测阈值固定值")]
        [JsonProperty("DW下限检测阈值固定值")]
        public float LLDetectionFDW { set; get; }
        [Category("参数DW判断配置"), DefaultValue(1.8), DisplayName("DW上限检测阈值百分比")]
        [JsonProperty("DW上限检测阈值百分比")]
        public float HLDetectionPDW { set; get; }
        [Category("参数DW判断配置"), DefaultValue(float.MaxValue), DisplayName("DW上限检测阈值固定值")]
        [JsonProperty("DW上限检测阈值固定值")]
        public float HLDetectionFDW { set; get; }

        [Category("参数亮度均匀性判断配置"), DefaultValue(false), DisplayName("亮度均匀性启用检测")]
        [JsonProperty("亮度均匀性启用检测")]
        public bool IsBUEnable { set; get; }
        [Category("参数亮度均匀性判断配置"), DefaultValue(0.2), DisplayName("亮度均匀性下限检测阈值百分比")]
        [JsonProperty("亮度均匀性下限检测阈值百分比")]
        public float LLDetectionPBU { set; get; }
        [Category("参数亮度均匀性判断配置"), DefaultValue(0), DisplayName("亮度均匀性下限检测阈值固定值")]
        [JsonProperty("亮度均匀性下限检测阈值固定值")]
        public float LLDetectionFBU { set; get; }
        [Category("参数亮度均匀性判断配置"), DefaultValue(1.8), DisplayName("亮度均匀性上限检测阈值百分比")]
        [JsonProperty("亮度均匀性上限检测阈值百分比")]
        public float HLDetectionPBU { set; get; }
        [Category("参数亮度均匀性判断配置"), DefaultValue(float.MaxValue), DisplayName("亮度均匀性上限检测阈值固定值")]
        [JsonProperty("亮度均匀性上限检测阈值固定值")]
        public float HLDetectionFBU { set; get; }

        [Category("参数色度均匀性判断配置"), DefaultValue(false), DisplayName("色度均匀性启用检测")]
        [JsonProperty("色度均匀性启用检测")]
        public bool IsCUEnable { set; get; }
        [Category("参数色度均匀性判断配置"), DefaultValue(0.2), DisplayName("色度均匀性下限检测阈值百分比")]
        [JsonProperty("色度均匀性下限检测阈值百分比")]
        public float LLDetectionPCU { set; get; }
        [Category("参数色度均匀性判断配置"), DefaultValue(0), DisplayName("色度均匀性下限检测阈值固定值")]
        [JsonProperty("色度均匀性下限检测阈值固定值")]
        public float LLDetectionFCU { set; get; }
        [Category("参数色度均匀性判断配置"), DefaultValue(1.8), DisplayName("色度均匀性上限检测阈值百分比")]
        [JsonProperty("色度均匀性上限检测阈值百分比")]
        public float HLDetectionPCU { set; get; }
        [Category("参数色度均匀性判断配置"), DefaultValue(float.MaxValue), DisplayName("色度均匀性上限检测阈值固定值")]
        [JsonProperty("色度均匀性上限检测阈值固定值")]
        public float HLDetectionFCU { set; get; }

        [Category("检测判断配置"), DisplayName("判断方式"), Description("1-仅通过百分比阈值判断\r\n2-仅通过固定阈值判断\r\n3-结合两种方式同时判断") ]
        [JsonProperty("判断方式")]
        public int JudgingWay { set; get; }

        [Category("检测判断配置"), DisplayName("多参数判断方式"), Description("1-选定参数中有一个判断NG即返回NG\r\n2-当所有选定参数都判定NG时返回NG")  ]
        [JsonProperty("多参数判断方式")]
        public int JudgingWayMul { set; get; }
    }


}
