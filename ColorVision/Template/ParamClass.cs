using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using Google.Protobuf.WellKnownTypes;
using HslCommunication.Secs.Types;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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

        [Category("设置"), DisplayName("序号")]
        public int ID { get => _ID; set { _ID = value; NotifyPropertyChanged(); } }
        private int _ID;

        private Dictionary<string, ModDetailModel> parameters;

        public ParamBase(int id)
        {
            this.ID = id;
            this.parameters = new Dictionary<string, ModDetailModel>();
        }
        public ParamBase(int id,List<ModDetailModel> detail)
        {
            this.ID = id;
            this.parameters = new Dictionary<string, ModDetailModel>();
            if (detail != null)
            {
                foreach (var flowDetailModel in detail)
                {
                    AddParameter(flowDetailModel.Symbol ?? "", flowDetailModel);
                }
            }
        }
        public void AddParameter(string key, ModDetailModel value)
        {
            parameters.Add(key, value);
        }
        internal void GetDetail(List<ModDetailModel> list)
        {
            list.AddRange(parameters.Values.ToList());
        }
        public void SetValue(string? value,[CallerMemberName] string propertyName = "")
        {
            if (parameters.ContainsKey(propertyName))
            {
                parameters[propertyName].ValueB = parameters[propertyName].ValueA;
                parameters[propertyName].ValueA = value;
            }
        }
        public T? GetValue<T> ([CallerMemberName] string propertyName = "")
        {
            string val = "";
            if (parameters.ContainsKey(propertyName)) val =  parameters[propertyName].ValueA;
            if (typeof(T) == typeof(int))
            {
                if(string.IsNullOrEmpty(val)) val = "0";
                return (T)(object)int.Parse(val);
            }else if (typeof(T) == typeof(string))
            {
                return (T)(object)val;
            }else if(typeof(T) == typeof(bool))
            {
                if (string.IsNullOrEmpty(val)) val = "False";
                return (T)(object)bool.Parse(val);
            }
            else if (typeof(T) == typeof(float))
            {
                if (string.IsNullOrEmpty(val)) val = "0.0";
                return (T)(object)float.Parse(val);
            }
            return (T)(object)val;
        }
    }


    /// <summary>
    /// 流程引擎模板
    /// </summary>
    public class FlowParam : ParamBase
    {
        public FlowParam():base(-1) {
        }
        public FlowParam(ModMasterModel dbModel, List<ModDetailModel> flowDetail) : base(dbModel.Id,flowDetail)
        {
            this.name = dbModel.Name ?? string.Empty;
        }

        private string name;
        public string Name { get => name; set { name = value; } }
        /// <summary>
        /// 流程文件名称
        /// </summary>
        public string? FileName
        {
            set { SetValue(value, "filename"); }
            get => GetValue<string>("filename");
        }
    }

    public class AoiParam: ParamBase
    {
        public AoiParam() : base(-1)
        {
            this.FilterByArea = true;
            this.MaxArea = 6000;
            this.MinArea = 10;
            this.FilterByContrast = true;
            this.MaxContrast = 1.7f;
            this.MinContrast = 0.3f;
            this.ContrastBrightness = 1.0f;
            this.ContrastDarkness = 0.5f;
            this.BlurSize = 19;
            this.MinContourSize = 5;
            this.ErodeSize = 5;
            this.DilateSize = 5;
            this.Left = 5;
            this.Right = 5;
            this.Top = 5;
            this.Bottom = 5;
        }


        public AoiParam(ModMasterModel aoiMaster, List<ModDetailModel> aoiDetail) : base(aoiMaster.Id,aoiDetail)
        {
        }

        public bool FilterByArea { set { SetValue(value.ToString()); } get => GetValue<bool>(); }
        public int MaxArea { set { SetValue(value.ToString()); } get => GetValue<int>(); }
        public int MinArea { set { SetValue(value.ToString()); } get => GetValue<int>(); }
        public bool FilterByContrast { set { SetValue(value.ToString()); } get => GetValue<bool>(); }
        public float MaxContrast { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        public float MinContrast { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        public float ContrastBrightness { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        public float ContrastDarkness { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        public int BlurSize { set { SetValue(value.ToString()); } get => GetValue<int>(); }
        public int MinContourSize { set { SetValue(value.ToString()); } get => GetValue<int>(); }
        public int ErodeSize { set { SetValue(value.ToString()); } get => GetValue<int>(); }
        public int DilateSize { set { SetValue(value.ToString()); } get => GetValue<int>(); }
        [Category("AoiRect")]
        public int Left { set { SetValue(value.ToString()); } get => GetValue<int>(); }
        [Category("AoiRect")]
        public int Right { set { SetValue(value.ToString()); } get => GetValue<int>(); }
        [Category("AoiRect")]
        public int Top { set { SetValue(value.ToString()); } get => GetValue<int>(); }
        [Category("AoiRect")]
        public int Bottom { set { SetValue(value.ToString()); } get => GetValue<int>(); }
    }
    public class LedReusltParam : ParamBase
    {
        public LedReusltParam() : base(-1)
        {
        }

        public LedReusltParam(ModMasterModel ledMaster, List<ModDetailModel> ledDetail) : base(ledMaster.Id, ledDetail)
        {
        }

        [Category("检测判断配置"), DefaultValue(1),DisplayName("灯珠抓取通道")]
        public int Channel { set { SetValue(value.ToString()); } get => GetValue<int>(); }
        [Category("检测判断配置"), DefaultValue(false)]
        public bool IsDebug { set { SetValue(value.ToString()); } get => GetValue<bool>(); }
        [Category("检测判断配置"), DefaultValue(true), DisplayName("是否下限检测")]
        public bool IsLDetection { set { SetValue(value.ToString()); } get => GetValue<bool>(); }
        [Category("检测判断配置"), DefaultValue(true), DisplayName("是否上限检测")]
        public bool IsHDetection { set { SetValue(value.ToString()); } get => GetValue<bool>(); }

        [Category("参数X判断配置"), DefaultValue(false),DisplayName("X启用检测")]
        public bool IsXEnable { set { SetValue(value.ToString()); } get => GetValue<bool>(); }
        [Category("参数X判断配置"), DefaultValue(0.2),DisplayName("X下限检测阈值百分比")]
        public float LLDetectionPX { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数X判断配置"), DefaultValue(0),DisplayName("X下限检测阈值固定值")]
        public float LLDetectionFX { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数X判断配置"), DefaultValue(1.8),DisplayName("X上限检测阈值百分比")]
        public float HLDetectionPX { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数X判断配置"), DefaultValue(float.MaxValue), DisplayName("X上限检测阈值固定值")]
        public float HLDetectionFX { set { SetValue(value.ToString()); } get => GetValue<float>(); }

        [Category("参数Y判断配置"), DefaultValue(false),DisplayName("Y启用检测")]
        public bool IsYEnable { set { SetValue(value.ToString()); } get => GetValue<bool>(); }
        [Category("参数Y判断配置"), DefaultValue(0.2), DisplayName("Y下限检测阈值百分比")]
        public float LLDetectionPY { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数Y判断配置"), DefaultValue(0),DisplayName("Y下限检测阈值固定值")]
        public float LLDetectionFY { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数Y判断配置"), DefaultValue(1.8), DisplayName("Y上限检测阈值百分比")]
        public float HLDetectionPY { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数Y判断配置"), DefaultValue(float.MaxValue), DisplayName("Y上限检测阈值固定值")]
        public float HLDetectionFY { set { SetValue(value.ToString()); } get => GetValue<float>(); }

        [Category("参数Z判断配置"), DefaultValue(false),DisplayName("Z启用检测")]
        public bool IsZEnable { set { SetValue(value.ToString()); } get => GetValue<bool>(); }
        [Category("参数Z判断配置"), DefaultValue(0.2), DisplayName("Z下限检测阈值百分比")]
        public float LLDetectionPZ { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数Z判断配置"), DefaultValue(0), DisplayName("Z下限检测阈值固定值")]
        public float LLDetectionFZ { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数Z判断配置"), DefaultValue(1.8), DisplayName("Z上限检测阈值百分比")]
        public float HLDetectionPZ { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数Z判断配置"), DefaultValue(float.MaxValue), DisplayName("Z上限检测阈值固定值")]
        public float HLDetectionFZ { set { SetValue(value.ToString()); } get => GetValue<float>(); }

        [Category("参数x判断配置"), DefaultValue(false), DisplayName("x启用检测")]
        public bool IslxEnable { set { SetValue(value.ToString()); } get => GetValue<bool>(); }
        [Category("参数x判断配置"), DefaultValue(0.2), DisplayName("x下限检测阈值百分比")]
        public float LLDetectionPlx { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数x判断配置"), DefaultValue(0), DisplayName("x下限检测阈值固定值")]
        public float LLDetectionFlx { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数x判断配置"), DefaultValue(1.8), DisplayName("x上限检测阈值百分比")]
        public float HLDetectionPlx { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数x判断配置"), DefaultValue(float.MaxValue), DisplayName("x上限检测阈值固定值")]
        public float HLDetectionFlx { set { SetValue(value.ToString()); } get => GetValue<float>(); }

        [Category("参数y判断配置"), DefaultValue(false), DisplayName("y启用检测")]
        public bool IslyEnable { set { SetValue(value.ToString()); } get => GetValue<bool>(); }
        [Category("参数y判断配置"), DefaultValue(0.2), DisplayName("y下限检测阈值百分比")]
        public float LLDetectionPly { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数y判断配置"), DefaultValue(0), DisplayName("y下限检测阈值固定值")]
        public float LLDetectionFly { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数y判断配置"), DefaultValue(1.8), DisplayName("y上限检测阈值百分比")]
        public float HLDetectionPly { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数y判断配置"), DefaultValue(float.MaxValue), DisplayName("y上限检测阈值固定值")]
        public float HLDetectionFly { set { SetValue(value.ToString()); } get => GetValue<float>(); }

        [Category("参数u判断配置"), DefaultValue(false), DisplayName("u启用检测")]
        public bool IsluEnable { set { SetValue(value.ToString()); } get => GetValue<bool>(); }
        [Category("参数u判断配置"), DefaultValue(0.2), DisplayName("u下限检测阈值百分比")]
        public float LLDetectionPlu { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数u判断配置"), DefaultValue(0), DisplayName("u下限检测阈值固定值")]
        public float LLDetectionFlu { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数u判断配置"), DefaultValue(1.8), DisplayName("u上限检测阈值百分比")]
        public float HLDetectionPlu { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数u判断配置"), DefaultValue(float.MaxValue), DisplayName("u上限检测阈值固定值")]
        public float HLDetectionFlu { set { SetValue(value.ToString()); } get => GetValue<float>(); }

        [Category("参数v判断配置"), DefaultValue(false), DisplayName("v启用检测")]
        public bool IslvEnable { set { SetValue(value.ToString()); } get => GetValue<bool>(); }
        [Category("参数v判断配置"), DefaultValue(0.2), DisplayName("v下限检测阈值百分比")]
        public float LLDetectionPlv { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数v判断配置"), DefaultValue(0), DisplayName("v下限检测阈值固定值")]
        public float LLDetectionFlv { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数v判断配置"), DefaultValue(1.8), DisplayName("v上限检测阈值百分比")]
        public float HLDetectionPlv { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数v判断配置"), DefaultValue(float.MaxValue), DisplayName("v上限检测阈值固定值")]
        public float HLDetectionFlv { set { SetValue(value.ToString()); } get => GetValue<float>(); }

        [Category("参数CCT判断配置"), DefaultValue(false), DisplayName("CCT启用检测")]
        public bool IsCCTEnable { set { SetValue(value.ToString()); } get => GetValue<bool>(); }
        [Category("参数CCT判断配置"), DefaultValue(0.2), DisplayName("CCT下限检测阈值百分比")]
        public float LLDetectionPCCT { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数CCT判断配置"), DefaultValue(0), DisplayName("CCT下限检测阈值固定值")]
        public float LLDetectionFCCT { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数CCT判断配置"), DefaultValue(1.8), DisplayName("CCT上限检测阈值百分比")]
        public float HLDetectionPCCT { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数CCT判断配置"), DefaultValue(float.MaxValue), DisplayName("CCT上限检测阈值固定值")]
        public float HLDetectionFCCT { set { SetValue(value.ToString()); } get => GetValue<float>(); }

        [Category("参数DW判断配置"), DefaultValue(false), DisplayName("DW启用检测")]
        public bool IsDWTEnable { set { SetValue(value.ToString()); } get => GetValue<bool>(); }
        [Category("参数DW判断配置"), DefaultValue(0.2), DisplayName("DW下限检测阈值百分比")]
        public float LLDetectionPDW { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数DW判断配置"), DefaultValue(0), DisplayName("DW下限检测阈值固定值")]
        public float LLDetectionFDW { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数DW判断配置"), DefaultValue(1.8), DisplayName("DW上限检测阈值百分比")]
        public float HLDetectionPDW { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数DW判断配置"), DefaultValue(float.MaxValue), DisplayName("DW上限检测阈值固定值")]
        public float HLDetectionFDW { set { SetValue(value.ToString()); } get => GetValue<float>(); }

        [Category("参数亮度均匀性判断配置"), DefaultValue(false), DisplayName("亮度均匀性启用检测")]
        public bool IsBUEnable { set { SetValue(value.ToString()); } get => GetValue<bool>(); }
        [Category("参数亮度均匀性判断配置"), DefaultValue(0.2), DisplayName("亮度均匀性下限检测阈值百分比")]
        public float LLDetectionPBU { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数亮度均匀性判断配置"), DefaultValue(0), DisplayName("亮度均匀性下限检测阈值固定值")]
        public float LLDetectionFBU { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数亮度均匀性判断配置"), DefaultValue(1.8), DisplayName("亮度均匀性上限检测阈值百分比")]
        public float HLDetectionPBU { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数亮度均匀性判断配置"), DefaultValue(float.MaxValue), DisplayName("亮度均匀性上限检测阈值固定值")]
        public float HLDetectionFBU { set { SetValue(value.ToString()); } get => GetValue<float>(); }

        [Category("参数色度均匀性判断配置"), DefaultValue(false), DisplayName("色度均匀性启用检测")]
        public bool IsCUEnable { set { SetValue(value.ToString()); } get => GetValue<bool>(); }
        [Category("参数色度均匀性判断配置"), DefaultValue(0.2), DisplayName("色度均匀性下限检测阈值百分比")]
        public float LLDetectionPCU { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数色度均匀性判断配置"), DefaultValue(0), DisplayName("色度均匀性下限检测阈值固定值")]
        public float LLDetectionFCU { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数色度均匀性判断配置"), DefaultValue(1.8), DisplayName("色度均匀性上限检测阈值百分比")]
        public float HLDetectionPCU { set { SetValue(value.ToString()); } get => GetValue<float>(); }
        [Category("参数色度均匀性判断配置"), DefaultValue(float.MaxValue), DisplayName("色度均匀性上限检测阈值固定值")]
        public float HLDetectionFCU { set { SetValue(value.ToString()); } get => GetValue<float>(); }

        [Category("检测判断配置"), DisplayName("判断方式"), Description("1-仅通过百分比阈值判断\r\n2-仅通过固定阈值判断\r\n3-结合两种方式同时判断") ]
        public int JudgingWay { set { SetValue(value.ToString()); } get => GetValue<int>(); }

        [Category("检测判断配置"), DisplayName("多参数判断方式"), Description("1-选定参数中有一个判断NG即返回NG\r\n2-当所有选定参数都判定NG时返回NG")  ]
        public int JudgingWayMul { set { SetValue(value.ToString()); } get => GetValue<int>(); }
    }


    public class CameraDeviceParam : ParamBase
    {
        public static int TypeValue = 1;
        public CameraDeviceParam() : base(-1)
        {
        }

        public CameraDeviceParam(ResourceModel dbModel) : base(dbModel.Id)
        {
            JsonValue = dbModel.Value;
        }

        public string? JsonValue { get; set; }
        public string? Code { get; set; }
    }

}
