#pragma warning disable CS8603  

using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.SettingUp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

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

        public ParamBase()
        {
            this.ID = -1;
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

        public ModDetailModel? GetParameter(string key)
        {
            if (parameters.TryGetValue(key,out ModDetailModel modDetailModel))
            {
                return modDetailModel;
            }
            else
            {
                return null;
            }
        }
        internal void GetDetail(List<ModDetailModel> list)
        {
            list.AddRange(parameters.Values.ToList());
        }

        protected override bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            storage = value;

            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
            {
                if (parameters.TryGetValue(propertyName,out ModDetailModel modDetailModel))
                {
                    modDetailModel.ValueB = modDetailModel.ValueA;
                    modDetailModel.ValueA = value?.ToString();
                }
            }
            NotifyPropertyChanged(propertyName);
            return true;
        }



        protected void SetProperty<T>(T value, [CallerMemberName] string propertyName = "")
        {
            if (parameters.TryGetValue(propertyName, out ModDetailModel modDetailModel))
            {
                modDetailModel.ValueB = modDetailModel.ValueA;
                modDetailModel.ValueA = value?.ToString();
            }
        }


        public T? GetValue<T>(T? storage, [CallerMemberName] string propertyName = "")
        {
            if (GlobalSetting.GetInstance().SoftwareConfig.IsUseMySql)
            {
                string val = "";
                if (parameters.TryGetValue(propertyName, out ModDetailModel modDetailModel))
                {
                    val = modDetailModel.ValueA;
                }
                if (typeof(T) == typeof(int))
                {
                    if (string.IsNullOrEmpty(val)) val = "0";
                    return (T)(object)int.Parse(val);
                }
                else if (typeof(T) == typeof(string))
                {
                    return (T)(object)val;
                }
                else if (typeof(T) == typeof(bool))
                {
                    if (string.IsNullOrEmpty(val)) val = "False";
                    return (T)(object)bool.Parse(val);
                }
                else if (typeof(T) == typeof(float))
                {
                    if (string.IsNullOrEmpty(val)) val = "0.0";
                    return (T)(object)float.Parse(val);
                }
                else if (typeof(T) == typeof(double))
                {
                    if (string.IsNullOrEmpty(val)) val = "0.0";
                    return (T)(object)double.Parse(val);
                }
                return (T)(object)val;
            }
            return storage;
        }

    }
    
    #pragma warning disable CA1707
    public class MTFParam : ParamBase
    {
        public MTFParam() { }
        public MTFParam(ModMasterModel aoiMaster, List<ModDetailModel> aoiDetail) : base(aoiMaster.Id, aoiDetail)
        {

        }

        [Category("MTF"), Description("MTF dRatio")]
        public double MTF_dRatio { get => GetValue(_MTF_dRatio); set { SetProperty(ref _MTF_dRatio, value); } }
        private double _MTF_dRatio =0.01;
    }


    /// <summary>
    /// 流程引擎模板
    /// </summary>
    public class FlowParam : ParamBase
    {
        public const string FileNameKey = "filename";
        public FlowParam() {
        }
        public FlowParam(ModMasterModel dbModel, List<ModDetailModel> flowDetail) : base(dbModel.Id,flowDetail)
        {
            this.name = dbModel.Name ?? string.Empty;
        }

        private string name;
        public string Name { get => name; set { name = value; } }

        private string dataBase64;
        public string DataBase64 { get => dataBase64; set { dataBase64 = value; } }

        /// <summary>
        /// 流程文件名称
        /// </summary>
        public string? FileName
        {
            set { SetProperty(ref _FileName, value?.ToString(), FileNameKey); }
            get => GetValue(_FileName, FileNameKey);
        }
        private string? _FileName;
    }

    public class AoiParam: ParamBase
    {
        public AoiParam() 
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

        public bool FilterByArea { set { SetProperty(ref _FilterByArea, value); } get => GetValue(_FilterByArea); }
        private bool _FilterByArea;

        public int MaxArea { set { SetProperty(ref _MaxArea, value); } get => GetValue(_MaxArea); }
        private int _MaxArea;

        public int MinArea { set { SetProperty(ref _MinArea, value); } get => GetValue(_MinArea); }
        private int _MinArea;

        public bool FilterByContrast { set { SetProperty(ref _FilterByContrast, value); } get => GetValue(_FilterByContrast); }
        private bool _FilterByContrast;

        public float MaxContrast { set { SetProperty(ref _MaxContrast, value); } get => GetValue(_MaxContrast); }
        private float _MaxContrast;
        public float MinContrast { set { SetProperty(ref _MinContrast, value); } get => GetValue(_MaxContrast); }
        private float _MinContrast;

        public float ContrastBrightness { set { SetProperty(ref _ContrastBrightness, value); } get => GetValue(_ContrastBrightness); }
        private float _ContrastBrightness;

        public float ContrastDarkness { set { SetProperty(ref _ContrastDarkness,value); } get => GetValue(_ContrastDarkness); }
        private float _ContrastDarkness;

        public int BlurSize { set { SetProperty(ref _BlurSize,value); } get => GetValue(_BlurSize); }
        private int _BlurSize;
        public int MinContourSize { set { SetProperty(ref _MinContourSize, value); } get => GetValue(_MinContourSize); }
        private int _MinContourSize;

        public int ErodeSize { set { SetProperty(ref _ErodeSize,value); } get => GetValue(_ErodeSize); }
        private int _ErodeSize;
        public int DilateSize { set { SetProperty(ref _DilateSize, value); } get => GetValue(_DilateSize); }
        private int _DilateSize;
        [Category("AoiRect")]
        public int Left { set { SetProperty(ref _Left,value); } get => GetValue(_Left); }
        private int _Left;

        [Category("AoiRect")]
        public int Right { set { SetProperty(ref _Right,value); } get => GetValue(_Right); }
        private int _Right;
        [Category("AoiRect")]
        public int Top { set { SetProperty(ref _Top, value); } get => GetValue(_Top); }
        private int _Top;
        [Category("AoiRect")]
        public int Bottom { set { SetProperty(ref _Bottom,value); } get => GetValue(_Bottom); }
        private int _Bottom;
    }

    public class LedReusltParam : ParamBase
    {
        public LedReusltParam() 
        {
        }

        public LedReusltParam(ModMasterModel ledMaster, List<ModDetailModel> ledDetail) : base(ledMaster.Id, ledDetail)
        {
        }

        [Category("检测判断配置"), DefaultValue(1),DisplayName("灯珠抓取通道")]
        public int Channel { set { SetProperty(ref _Channel, value); } get => GetValue(_Channel); }
        private int _Channel;
        [Category("检测判断配置"), DefaultValue(false)]
        public bool IsDebug { set { SetProperty(ref _IsDebug, value); } get => GetValue(_IsDebug); }
        private bool _IsDebug;
        [Category("检测判断配置"), DefaultValue(true), DisplayName("是否下限检测")]
        public bool IsLDetection { set { SetProperty(ref _IsLDetection,value); } get => GetValue(_IsLDetection); }
        private bool _IsLDetection;

        [Category("检测判断配置"), DefaultValue(true), DisplayName("是否上限检测")]
        public bool IsHDetection { set { SetProperty(ref _IsHDetection,value); } get => GetValue(_IsHDetection); }
        private bool _IsHDetection;

        [Category("参数X判断配置"), DefaultValue(false),DisplayName("X启用检测")]
        public bool IsXEnable { set { SetProperty(ref _IsXEnable,value); } get => GetValue(_IsXEnable); }
        private bool _IsXEnable;

        [Category("参数X判断配置"), DefaultValue(0.2),DisplayName("X下限检测阈值百分比")]
        public float LLDetectionPX { set { SetProperty(ref _LLDetectionPX,value); } get => GetValue(_LLDetectionPX); }
        private float _LLDetectionPX;

        [Category("参数X判断配置"), DefaultValue(0),DisplayName("X下限检测阈值固定值")]
        public float LLDetectionFX { set { SetProperty(ref _LLDetectionFX ,value); } get => GetValue(_LLDetectionFX); }
        private float _LLDetectionFX;


        [Category("参数X判断配置"), DefaultValue(1.8),DisplayName("X上限检测阈值百分比")]
        public float HLDetectionPX { set { SetProperty(ref _HLDetectionPX,value); } get => GetValue(_HLDetectionPX); }
        private float _HLDetectionPX;

        [Category("参数X判断配置"), DefaultValue(float.MaxValue), DisplayName("X上限检测阈值固定值")]
        public float HLDetectionFX { set { SetProperty(ref _HLDetectionFX,value); } get => GetValue(_HLDetectionFX); }
        private float _HLDetectionFX;

        [Category("参数Y判断配置"), DefaultValue(false),DisplayName("Y启用检测")]
        public bool IsYEnable { set { SetProperty(ref _IsYEnable,value); } get => GetValue(_IsYEnable); }
        private bool _IsYEnable;

        [Category("参数Y判断配置"), DefaultValue(0.2), DisplayName("Y下限检测阈值百分比")]
        public float LLDetectionPY { set { SetProperty(ref _LLDetectionPY,value); } get => GetValue(_LLDetectionPY); }
        private float _LLDetectionPY;

        [Category("参数Y判断配置"), DefaultValue(0),DisplayName("Y下限检测阈值固定值")]
        public float LLDetectionFY { set { SetProperty(ref _LLDetectionFY,value); } get => GetValue(_LLDetectionFY); }
        private float _LLDetectionFY;

        [Category("参数Y判断配置"), DefaultValue(1.8), DisplayName("Y上限检测阈值百分比")]
        public float HLDetectionPY { set { SetProperty(ref _HLDetectionPY,value); } get => GetValue(_HLDetectionPY); }
        private float _HLDetectionPY;

        [Category("参数Y判断配置"), DefaultValue(float.MaxValue), DisplayName("Y上限检测阈值固定值")]
        public float HLDetectionFY { set { SetProperty(ref _HLDetectionFY,value); } get => GetValue(_HLDetectionFY); }
        private float _HLDetectionFY;

        [Category("参数Z判断配置"), DefaultValue(false),DisplayName("Z启用检测")]
        public bool IsZEnable { set { SetProperty(ref _IsZEnable,value); } get => GetValue(_IsZEnable); }
        private bool _IsZEnable;

        [Category("参数Z判断配置"), DefaultValue(0.2), DisplayName("Z下限检测阈值百分比")]
        public float LLDetectionPZ { set { SetProperty(ref _LLDetectionPZ,value); } get => GetValue(_LLDetectionPZ); }
        private float _LLDetectionPZ;

        [Category("参数Z判断配置"), DefaultValue(0), DisplayName("Z下限检测阈值固定值")]
        public float LLDetectionFZ { set { SetProperty(ref _LLDetectionFZ,value); } get => GetValue(_LLDetectionFZ); }
        private float _LLDetectionFZ;

        [Category("参数Z判断配置"), DefaultValue(1.8), DisplayName("Z上限检测阈值百分比")]
        public float HLDetectionPZ { set { SetProperty(ref _HLDetectionPZ,value); } get => GetValue(_HLDetectionPZ); }
        private float _HLDetectionPZ;

        [Category("参数Z判断配置"), DefaultValue(float.MaxValue), DisplayName("Z上限检测阈值固定值")]
        public float HLDetectionFZ { set { SetProperty(ref _HLDetectionFZ,value); } get => GetValue(_HLDetectionFZ); }
        private float _HLDetectionFZ;

        [Category("参数x判断配置"), DefaultValue(false), DisplayName("x启用检测")]
        public bool IslxEnable { set { SetProperty(ref _IslxEnable,value); } get => GetValue(_IslxEnable); }
        private bool _IslxEnable;
        [Category("参数x判断配置"), DefaultValue(0.2), DisplayName("x下限检测阈值百分比")]
        public float LLDetectionPlx { set { SetProperty(ref _LLDetectionPlx,value); } get => GetValue(_LLDetectionPlx); }
        private float _LLDetectionPlx;
        [Category("参数x判断配置"), DefaultValue(0), DisplayName("x下限检测阈值固定值")]
        public float LLDetectionFlx { set { SetProperty(ref _LLDetectionFlx,value); } get => GetValue(_LLDetectionFlx); }
        private float _LLDetectionFlx;
        [Category("参数x判断配置"), DefaultValue(1.8), DisplayName("x上限检测阈值百分比")]
        public float HLDetectionPlx { set { SetProperty(ref _HLDetectionPlx,value); } get => GetValue(_HLDetectionPlx); }
        private float _HLDetectionPlx;

        [Category("参数x判断配置"), DefaultValue(float.MaxValue), DisplayName("x上限检测阈值固定值")]
        public float HLDetectionFlx { set { SetProperty(ref _HLDetectionFlx,value); } get => GetValue(_HLDetectionFlx); }
        private float _HLDetectionFlx;

        [Category("参数y判断配置"), DefaultValue(false), DisplayName("y启用检测")]
        public bool IslyEnable { set { SetProperty(ref _IslyEnable,value); } get => GetValue(_IslyEnable); }
        private bool _IslyEnable;

        [Category("参数y判断配置"), DefaultValue(0.2), DisplayName("y下限检测阈值百分比")]
        public float LLDetectionPly { set { SetProperty(ref _LLDetectionPly,value); } get => GetValue(_LLDetectionPly); }
        private float _LLDetectionPly;

        [Category("参数y判断配置"), DefaultValue(0), DisplayName("y下限检测阈值固定值")]
        public float LLDetectionFly { set { SetProperty(ref _LLDetectionFly,value); } get => GetValue(_LLDetectionFly); }
        private float _LLDetectionFly;

        [Category("参数y判断配置"), DefaultValue(1.8), DisplayName("y上限检测阈值百分比")]
        public float HLDetectionPly { set { SetProperty(ref _HLDetectionPly,value); } get => GetValue(_HLDetectionPly); }
        private float _HLDetectionPly;

        [Category("参数y判断配置"), DefaultValue(float.MaxValue), DisplayName("y上限检测阈值固定值")]
        public float HLDetectionFly { set { SetProperty(ref _HLDetectionFly,value); } get => GetValue(_HLDetectionFly); }
        private float _HLDetectionFly;

        [Category("参数u判断配置"), DefaultValue(false), DisplayName("u启用检测")]
        public bool IsluEnable { set { SetProperty(ref _IsluEnable,value); } get => GetValue(_IsluEnable); }
        private bool _IsluEnable;

        [Category("参数u判断配置"), DefaultValue(0.2), DisplayName("u下限检测阈值百分比")]
        public float LLDetectionPlu { set { SetProperty(ref _LLDetectionPlu,value); } get => GetValue(_LLDetectionPlu); }
        private float _LLDetectionPlu;

        [Category("参数u判断配置"), DefaultValue(0), DisplayName("u下限检测阈值固定值")]
        public float LLDetectionFlu { set { SetProperty(ref _LLDetectionFlu,value); } get => GetValue(_LLDetectionFlu); }
        private float _LLDetectionFlu;

        [Category("参数u判断配置"), DefaultValue(1.8), DisplayName("u上限检测阈值百分比")]
        public float HLDetectionPlu { set { SetProperty(ref _HLDetectionPlu,value); } get => GetValue(_HLDetectionPlu); }
        private float _HLDetectionPlu;

        [Category("参数u判断配置"), DefaultValue(float.MaxValue), DisplayName("u上限检测阈值固定值")]
        public float HLDetectionFlu { set { SetProperty(ref _HLDetectionFlu,value); } get => GetValue(_HLDetectionFlu); }
        private float _HLDetectionFlu;

        [Category("参数v判断配置"), DefaultValue(false), DisplayName("v启用检测")]
        public bool IslvEnable { set { SetProperty(ref _IslvEnable,value); } get => GetValue(_IslvEnable); }
        private bool _IslvEnable;

        [Category("参数v判断配置"), DefaultValue(0.2), DisplayName("v下限检测阈值百分比")]
        public float LLDetectionPlv { set { SetProperty(ref _LLDetectionPlv,value); } get => GetValue(_LLDetectionPlv); }
        private float _LLDetectionPlv;

        [Category("参数v判断配置"), DefaultValue(0), DisplayName("v下限检测阈值固定值")]
        public float LLDetectionFlv { set { SetProperty(ref _LLDetectionFlv,value); } get => GetValue(_LLDetectionFlv); }
        private float _LLDetectionFlv;

        [Category("参数v判断配置"), DefaultValue(1.8), DisplayName("v上限检测阈值百分比")]
        public float HLDetectionPlv { set { SetProperty(ref _HLDetectionPlv,value); } get => GetValue(_HLDetectionPlv); }
        private float _HLDetectionPlv;

        [Category("参数v判断配置"), DefaultValue(float.MaxValue), DisplayName("v上限检测阈值固定值")]
        public float HLDetectionFlv { set { SetProperty(ref _HLDetectionFlv,value); } get => GetValue(_HLDetectionFlv); }
        private float _HLDetectionFlv;

        [Category("参数CCT判断配置"), DefaultValue(false), DisplayName("CCT启用检测")]
        public bool IsCCTEnable { set { SetProperty(ref _IsCCTEnable,value); } get => GetValue(_IsCCTEnable); }
        private bool _IsCCTEnable;

        [Category("参数CCT判断配置"), DefaultValue(0.2), DisplayName("CCT下限检测阈值百分比")]
        public float LLDetectionPCCT { set { SetProperty(ref _LLDetectionPCCT,value); } get => GetValue(_LLDetectionPCCT); }
        private float _LLDetectionPCCT;
        [Category("参数CCT判断配置"), DefaultValue(0), DisplayName("CCT下限检测阈值固定值")]
        public float LLDetectionFCCT { set { SetProperty(ref _LLDetectionFCCT,value); } get => GetValue(_LLDetectionFCCT); }
        private float _LLDetectionFCCT;

        [Category("参数CCT判断配置"), DefaultValue(1.8), DisplayName("CCT上限检测阈值百分比")]
        public float HLDetectionPCCT { set { SetProperty(ref _HLDetectionPCCT,value); } get => GetValue(_HLDetectionPCCT); }
        private float _HLDetectionPCCT;
        [Category("参数CCT判断配置"), DefaultValue(float.MaxValue), DisplayName("CCT上限检测阈值固定值")]
        public float HLDetectionFCCT { set { SetProperty(ref _HLDetectionFCCT,value); } get => GetValue(_HLDetectionFCCT); }
        private float _HLDetectionFCCT;

        [Category("参数DW判断配置"), DefaultValue(false), DisplayName("DW启用检测")]
        public bool IsDWTEnable { set { SetProperty(ref _IsDWTEnable,value); } get => GetValue(_IsDWTEnable); }
        private bool _IsDWTEnable;

        [Category("参数DW判断配置"), DefaultValue(0.2), DisplayName("DW下限检测阈值百分比")]
        public float LLDetectionPDW { set { SetProperty(ref _LLDetectionPDW,value); } get => GetValue(_LLDetectionPDW); }
        private float _LLDetectionPDW;

        [Category("参数DW判断配置"), DefaultValue(0), DisplayName("DW下限检测阈值固定值")]
        public float LLDetectionFDW { set { SetProperty(ref _LLDetectionFDW,value); } get => GetValue(_LLDetectionFDW); }
        private float _LLDetectionFDW;

        [Category("参数DW判断配置"), DefaultValue(1.8), DisplayName("DW上限检测阈值百分比")]
        public float HLDetectionPDW { set { SetProperty(ref _HLDetectionPDW,value); } get => GetValue(_HLDetectionPDW); }
        private float _HLDetectionPDW;

        [Category("参数DW判断配置"), DefaultValue(float.MaxValue), DisplayName("DW上限检测阈值固定值")]
        public float HLDetectionFDW { set { SetProperty(ref _HLDetectionFDW,value); } get => GetValue(_HLDetectionFDW); }
        private float _HLDetectionFDW;

        [Category("参数亮度均匀性判断配置"), DefaultValue(false), DisplayName("亮度均匀性启用检测")]
        public bool IsBUEnable { set { SetProperty(ref _IsBUEnable,value); } get => GetValue(_IsBUEnable); }
        private bool _IsBUEnable;
        [Category("参数亮度均匀性判断配置"), DefaultValue(0.2), DisplayName("亮度均匀性下限检测阈值百分比")]
        public float LLDetectionPBU { set { SetProperty(ref _LLDetectionPBU,value); } get => GetValue(_LLDetectionPBU); }
        private float _LLDetectionPBU;

        [Category("参数亮度均匀性判断配置"), DefaultValue(0), DisplayName("亮度均匀性下限检测阈值固定值")]
        public float LLDetectionFBU { set { SetProperty(ref _LLDetectionFBU,value); } get => GetValue(_LLDetectionFBU); }
        private float _LLDetectionFBU;
        [Category("参数亮度均匀性判断配置"), DefaultValue(1.8), DisplayName("亮度均匀性上限检测阈值百分比")]
        public float HLDetectionPBU { set { SetProperty(ref _HLDetectionPBU,value); } get => GetValue(_HLDetectionPBU); }
        private float _HLDetectionPBU;

        [Category("参数亮度均匀性判断配置"), DefaultValue(float.MaxValue), DisplayName("亮度均匀性上限检测阈值固定值")]
        public float HLDetectionFBU { set { SetProperty(ref _HLDetectionFBU,value); } get => GetValue(_HLDetectionFBU); }
        private float _HLDetectionFBU;

        [Category("参数色度均匀性判断配置"), DefaultValue(false), DisplayName("色度均匀性启用检测")]
        public bool IsCUEnable { set { SetProperty(ref _IsCUEnable,value); } get => GetValue(_IsCUEnable); }
        private bool _IsCUEnable;

        [Category("参数色度均匀性判断配置"), DefaultValue(0.2), DisplayName("色度均匀性下限检测阈值百分比")]
        public float LLDetectionPCU { set { SetProperty(ref _LLDetectionPCU,value); } get => GetValue(_LLDetectionPCU); }
        private float _LLDetectionPCU;

        [Category("参数色度均匀性判断配置"), DefaultValue(0), DisplayName("色度均匀性下限检测阈值固定值")]
        public float LLDetectionFCU { set { SetProperty(ref _LLDetectionFCU,value); } get => GetValue(_LLDetectionFCU); }
        private float _LLDetectionFCU;

        [Category("参数色度均匀性判断配置"), DefaultValue(1.8), DisplayName("色度均匀性上限检测阈值百分比")]
        public float HLDetectionPCU { set { SetProperty(ref _HLDetectionPCU,value); } get => GetValue(_HLDetectionPCU); }
        private float _HLDetectionPCU;

        [Category("参数色度均匀性判断配置"), DefaultValue(float.MaxValue), DisplayName("色度均匀性上限检测阈值固定值")]
        public float HLDetectionFCU { set { SetProperty(ref _HLDetectionFCU,value); } get => GetValue(_HLDetectionFCU); }
        private float _HLDetectionFCU;

        [Category("检测判断配置"), DisplayName("判断方式"), Description("1-仅通过百分比阈值判断\r\n2-仅通过固定阈值判断\r\n3-结合两种方式同时判断") ]
        public int JudgingWay { set { SetProperty(ref _JudgingWay,value); } get => GetValue(_JudgingWay); }
        private int _JudgingWay;

        [Category("检测判断配置"), DisplayName("多参数判断方式"), Description("1-选定参数中有一个判断NG即返回NG\r\n2-当所有选定参数都判定NG时返回NG")  ]
        public int JudgingWayMul { set { SetProperty(ref _JudgingWayMul,value); } get => GetValue(_JudgingWayMul); }
        private int _JudgingWayMul;
    }


    public class MeasureParam : ParamBase
    {
        public MeasureParam(MeasureMasterModel dbModel)
        {
            this.ID = dbModel.Id;
            this.IsEnable = dbModel.IsEnable;
        }
    }
    public class ResourceParam : ParamBase
    {
        public static int TypeValue { get; set; } = 1;

        public ResourceParam()
        {

        }

        public ResourceParam(SysResourceModel dbModel) 
        {
            this.ID =dbModel.Id;
            JsonValue = dbModel.Value;
        }

        public string? JsonValue { get; set; }
        public string? Code { get; set; }
    }

    public class PGParam : ParamBase
    {
        public PGParam()
        {
        }

        public PGParam(ModMasterModel pgMaster, List<ModDetailModel> pgDetail) : base(pgMaster.Id, pgDetail)
        {

        }
        public const string StartKey = "CM_StartPG";
        public const string StopKey = "CM_StopPG";
        public const string ReSetKey = "CM_ReSetPG";
        public const string SwitchUpKey = "CM_SwitchUpPG";
        public const string SwitchDownKey = "CM_SwitchDownPG";
        public const string SwitchFrameKey = "CM_SwitchFramePG";
        public const string CustomKey = "CM_CustomCmd";
        //PG开始指令
        public string StartPG
        {
            set { SetProperty(ref _CM_StartPG, value, StartKey); }
            get => GetValue(_CM_StartPG, StartKey);
        }
        private string _CM_StartPG;
        //PG停止指令
        public string StopPG
        {
            set { SetProperty(ref _CM_StopPG, value, StopKey); }
            get => GetValue(_CM_StopPG, StopKey);
        }
        private string _CM_StopPG;
        //PG重置指令
        public string ReSetPG
        {
            set { SetProperty(ref _CM_ReSetPG, value, ReSetKey); }
            get => GetValue(_CM_ReSetPG, ReSetKey);
        }
        private string _CM_ReSetPG;
        //PG上指令
        public string SwitchUpPG
        {
            set { SetProperty(ref _CM_SwitchUpPG, value, SwitchUpKey); }
            get => GetValue(_CM_SwitchUpPG, SwitchUpKey);
        }
        private string _CM_SwitchUpPG;

        //PG下指令
        public string SwitchDownPG
        {
            set { SetProperty(ref _CM_SwitchDownPG, value, SwitchDownKey); }
            get => GetValue(_CM_SwitchDownPG, SwitchDownKey);
        }
        private string _CM_SwitchDownPG;
        //PG切指定指令
        public string SwitchFramePG
        {
            set { SetProperty(ref _CM_SwitchFramePG, value, SwitchFrameKey); }
            get => GetValue(_CM_SwitchFramePG, SwitchFrameKey);
        }
        private string _CM_SwitchFramePG;

        public Dictionary<string, string> ConvertToMap()
        {
            Dictionary<string, string> result= new Dictionary<string, string>();
            result.Add(StartKey, StartPG);
            result.Add(StopKey, StopPG);
            result.Add(ReSetKey, ReSetPG);
            result.Add(SwitchUpKey, SwitchUpPG);
            result.Add(SwitchDownKey, SwitchDownPG);
            result.Add(SwitchFrameKey, SwitchFramePG);
            result.Add(CustomKey, "");
            return result;
        }
    }
}
