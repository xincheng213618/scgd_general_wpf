using ColorVision.MySql.DAO;
using ColorVision.Templates;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Services.Devices.Algorithm.Templates
{
    public class LedReusltParam : ParamBase
    {
        public LedReusltParam()
        {
        }

        public LedReusltParam(ModMasterModel modMaster, List<ModDetailModel> ledDetail) : base(modMaster.Id, modMaster.Name ?? string.Empty, ledDetail)
        {
        }

        [Category("检测判断配置"), DefaultValue(1), DisplayName("灯珠抓取通道")]
        public int Channel { set { SetProperty(ref _Channel, value); } get => GetValue(_Channel); }
        private int _Channel;
        [Category("检测判断配置"), DefaultValue(false)]
        public bool IsDebug { set { SetProperty(ref _IsDebug, value); } get => GetValue(_IsDebug); }
        private bool _IsDebug;
        [Category("检测判断配置"), DefaultValue(true), DisplayName("是否下限检测")]
        public bool IsLDetection { set { SetProperty(ref _IsLDetection, value); } get => GetValue(_IsLDetection); }
        private bool _IsLDetection;

        [Category("检测判断配置"), DefaultValue(true), DisplayName("是否上限检测")]
        public bool IsHDetection { set { SetProperty(ref _IsHDetection, value); } get => GetValue(_IsHDetection); }
        private bool _IsHDetection;

        [Category("参数X判断配置"), DefaultValue(false), DisplayName("X启用检测")]
        public bool IsXEnable { set { SetProperty(ref _IsXEnable, value); } get => GetValue(_IsXEnable); }
        private bool _IsXEnable;

        [Category("参数X判断配置"), DefaultValue(0.2), DisplayName("X下限检测阈值百分比")]
        public float LLDetectionPX { set { SetProperty(ref _LLDetectionPX, value); } get => GetValue(_LLDetectionPX); }
        private float _LLDetectionPX;

        [Category("参数X判断配置"), DefaultValue(0), DisplayName("X下限检测阈值固定值")]
        public float LLDetectionFX { set { SetProperty(ref _LLDetectionFX, value); } get => GetValue(_LLDetectionFX); }
        private float _LLDetectionFX;


        [Category("参数X判断配置"), DefaultValue(1.8), DisplayName("X上限检测阈值百分比")]
        public float HLDetectionPX { set { SetProperty(ref _HLDetectionPX, value); } get => GetValue(_HLDetectionPX); }
        private float _HLDetectionPX;

        [Category("参数X判断配置"), DefaultValue(float.MaxValue), DisplayName("X上限检测阈值固定值")]
        public float HLDetectionFX { set { SetProperty(ref _HLDetectionFX, value); } get => GetValue(_HLDetectionFX); }
        private float _HLDetectionFX;

        [Category("参数Y判断配置"), DefaultValue(false), DisplayName("Y启用检测")]
        public bool IsYEnable { set { SetProperty(ref _IsYEnable, value); } get => GetValue(_IsYEnable); }
        private bool _IsYEnable;

        [Category("参数Y判断配置"), DefaultValue(0.2), DisplayName("Y下限检测阈值百分比")]
        public float LLDetectionPY { set { SetProperty(ref _LLDetectionPY, value); } get => GetValue(_LLDetectionPY); }
        private float _LLDetectionPY;

        [Category("参数Y判断配置"), DefaultValue(0), DisplayName("Y下限检测阈值固定值")]
        public float LLDetectionFY { set { SetProperty(ref _LLDetectionFY, value); } get => GetValue(_LLDetectionFY); }
        private float _LLDetectionFY;

        [Category("参数Y判断配置"), DefaultValue(1.8), DisplayName("Y上限检测阈值百分比")]
        public float HLDetectionPY { set { SetProperty(ref _HLDetectionPY, value); } get => GetValue(_HLDetectionPY); }
        private float _HLDetectionPY;

        [Category("参数Y判断配置"), DefaultValue(float.MaxValue), DisplayName("Y上限检测阈值固定值")]
        public float HLDetectionFY { set { SetProperty(ref _HLDetectionFY, value); } get => GetValue(_HLDetectionFY); }
        private float _HLDetectionFY;

        [Category("参数Z判断配置"), DefaultValue(false), DisplayName("Z启用检测")]
        public bool IsZEnable { set { SetProperty(ref _IsZEnable, value); } get => GetValue(_IsZEnable); }
        private bool _IsZEnable;

        [Category("参数Z判断配置"), DefaultValue(0.2), DisplayName("Z下限检测阈值百分比")]
        public float LLDetectionPZ { set { SetProperty(ref _LLDetectionPZ, value); } get => GetValue(_LLDetectionPZ); }
        private float _LLDetectionPZ;

        [Category("参数Z判断配置"), DefaultValue(0), DisplayName("Z下限检测阈值固定值")]
        public float LLDetectionFZ { set { SetProperty(ref _LLDetectionFZ, value); } get => GetValue(_LLDetectionFZ); }
        private float _LLDetectionFZ;

        [Category("参数Z判断配置"), DefaultValue(1.8), DisplayName("Z上限检测阈值百分比")]
        public float HLDetectionPZ { set { SetProperty(ref _HLDetectionPZ, value); } get => GetValue(_HLDetectionPZ); }
        private float _HLDetectionPZ;

        [Category("参数Z判断配置"), DefaultValue(float.MaxValue), DisplayName("Z上限检测阈值固定值")]
        public float HLDetectionFZ { set { SetProperty(ref _HLDetectionFZ, value); } get => GetValue(_HLDetectionFZ); }
        private float _HLDetectionFZ;

        [Category("参数x判断配置"), DefaultValue(false), DisplayName("x启用检测")]
        public bool IslxEnable { set { SetProperty(ref _IslxEnable, value); } get => GetValue(_IslxEnable); }
        private bool _IslxEnable;
        [Category("参数x判断配置"), DefaultValue(0.2), DisplayName("x下限检测阈值百分比")]
        public float LLDetectionPlx { set { SetProperty(ref _LLDetectionPlx, value); } get => GetValue(_LLDetectionPlx); }
        private float _LLDetectionPlx;
        [Category("参数x判断配置"), DefaultValue(0), DisplayName("x下限检测阈值固定值")]
        public float LLDetectionFlx { set { SetProperty(ref _LLDetectionFlx, value); } get => GetValue(_LLDetectionFlx); }
        private float _LLDetectionFlx;
        [Category("参数x判断配置"), DefaultValue(1.8), DisplayName("x上限检测阈值百分比")]
        public float HLDetectionPlx { set { SetProperty(ref _HLDetectionPlx, value); } get => GetValue(_HLDetectionPlx); }
        private float _HLDetectionPlx;

        [Category("参数x判断配置"), DefaultValue(float.MaxValue), DisplayName("x上限检测阈值固定值")]
        public float HLDetectionFlx { set { SetProperty(ref _HLDetectionFlx, value); } get => GetValue(_HLDetectionFlx); }
        private float _HLDetectionFlx;

        [Category("参数y判断配置"), DefaultValue(false), DisplayName("y启用检测")]
        public bool IslyEnable { set { SetProperty(ref _IslyEnable, value); } get => GetValue(_IslyEnable); }
        private bool _IslyEnable;

        [Category("参数y判断配置"), DefaultValue(0.2), DisplayName("y下限检测阈值百分比")]
        public float LLDetectionPly { set { SetProperty(ref _LLDetectionPly, value); } get => GetValue(_LLDetectionPly); }
        private float _LLDetectionPly;

        [Category("参数y判断配置"), DefaultValue(0), DisplayName("y下限检测阈值固定值")]
        public float LLDetectionFly { set { SetProperty(ref _LLDetectionFly, value); } get => GetValue(_LLDetectionFly); }
        private float _LLDetectionFly;

        [Category("参数y判断配置"), DefaultValue(1.8), DisplayName("y上限检测阈值百分比")]
        public float HLDetectionPly { set { SetProperty(ref _HLDetectionPly, value); } get => GetValue(_HLDetectionPly); }
        private float _HLDetectionPly;

        [Category("参数y判断配置"), DefaultValue(float.MaxValue), DisplayName("y上限检测阈值固定值")]
        public float HLDetectionFly { set { SetProperty(ref _HLDetectionFly, value); } get => GetValue(_HLDetectionFly); }
        private float _HLDetectionFly;

        [Category("参数u判断配置"), DefaultValue(false), DisplayName("u启用检测")]
        public bool IsluEnable { set { SetProperty(ref _IsluEnable, value); } get => GetValue(_IsluEnable); }
        private bool _IsluEnable;

        [Category("参数u判断配置"), DefaultValue(0.2), DisplayName("u下限检测阈值百分比")]
        public float LLDetectionPlu { set { SetProperty(ref _LLDetectionPlu, value); } get => GetValue(_LLDetectionPlu); }
        private float _LLDetectionPlu;

        [Category("参数u判断配置"), DefaultValue(0), DisplayName("u下限检测阈值固定值")]
        public float LLDetectionFlu { set { SetProperty(ref _LLDetectionFlu, value); } get => GetValue(_LLDetectionFlu); }
        private float _LLDetectionFlu;

        [Category("参数u判断配置"), DefaultValue(1.8), DisplayName("u上限检测阈值百分比")]
        public float HLDetectionPlu { set { SetProperty(ref _HLDetectionPlu, value); } get => GetValue(_HLDetectionPlu); }
        private float _HLDetectionPlu;

        [Category("参数u判断配置"), DefaultValue(float.MaxValue), DisplayName("u上限检测阈值固定值")]
        public float HLDetectionFlu { set { SetProperty(ref _HLDetectionFlu, value); } get => GetValue(_HLDetectionFlu); }
        private float _HLDetectionFlu;

        [Category("参数v判断配置"), DefaultValue(false), DisplayName("v启用检测")]
        public bool IslvEnable { set { SetProperty(ref _IslvEnable, value); } get => GetValue(_IslvEnable); }
        private bool _IslvEnable;

        [Category("参数v判断配置"), DefaultValue(0.2), DisplayName("v下限检测阈值百分比")]
        public float LLDetectionPlv { set { SetProperty(ref _LLDetectionPlv, value); } get => GetValue(_LLDetectionPlv); }
        private float _LLDetectionPlv;

        [Category("参数v判断配置"), DefaultValue(0), DisplayName("v下限检测阈值固定值")]
        public float LLDetectionFlv { set { SetProperty(ref _LLDetectionFlv, value); } get => GetValue(_LLDetectionFlv); }
        private float _LLDetectionFlv;

        [Category("参数v判断配置"), DefaultValue(1.8), DisplayName("v上限检测阈值百分比")]
        public float HLDetectionPlv { set { SetProperty(ref _HLDetectionPlv, value); } get => GetValue(_HLDetectionPlv); }
        private float _HLDetectionPlv;

        [Category("参数v判断配置"), DefaultValue(float.MaxValue), DisplayName("v上限检测阈值固定值")]
        public float HLDetectionFlv { set { SetProperty(ref _HLDetectionFlv, value); } get => GetValue(_HLDetectionFlv); }
        private float _HLDetectionFlv;

        [Category("参数CCT判断配置"), DefaultValue(false), DisplayName("CCT启用检测")]
        public bool IsCCTEnable { set { SetProperty(ref _IsCCTEnable, value); } get => GetValue(_IsCCTEnable); }
        private bool _IsCCTEnable;

        [Category("参数CCT判断配置"), DefaultValue(0.2), DisplayName("CCT下限检测阈值百分比")]
        public float LLDetectionPCCT { set { SetProperty(ref _LLDetectionPCCT, value); } get => GetValue(_LLDetectionPCCT); }
        private float _LLDetectionPCCT;
        [Category("参数CCT判断配置"), DefaultValue(0), DisplayName("CCT下限检测阈值固定值")]
        public float LLDetectionFCCT { set { SetProperty(ref _LLDetectionFCCT, value); } get => GetValue(_LLDetectionFCCT); }
        private float _LLDetectionFCCT;

        [Category("参数CCT判断配置"), DefaultValue(1.8), DisplayName("CCT上限检测阈值百分比")]
        public float HLDetectionPCCT { set { SetProperty(ref _HLDetectionPCCT, value); } get => GetValue(_HLDetectionPCCT); }
        private float _HLDetectionPCCT;
        [Category("参数CCT判断配置"), DefaultValue(float.MaxValue), DisplayName("CCT上限检测阈值固定值")]
        public float HLDetectionFCCT { set { SetProperty(ref _HLDetectionFCCT, value); } get => GetValue(_HLDetectionFCCT); }
        private float _HLDetectionFCCT;

        [Category("参数DW判断配置"), DefaultValue(false), DisplayName("DW启用检测")]
        public bool IsDWTEnable { set { SetProperty(ref _IsDWTEnable, value); } get => GetValue(_IsDWTEnable); }
        private bool _IsDWTEnable;

        [Category("参数DW判断配置"), DefaultValue(0.2), DisplayName("DW下限检测阈值百分比")]
        public float LLDetectionPDW { set { SetProperty(ref _LLDetectionPDW, value); } get => GetValue(_LLDetectionPDW); }
        private float _LLDetectionPDW;

        [Category("参数DW判断配置"), DefaultValue(0), DisplayName("DW下限检测阈值固定值")]
        public float LLDetectionFDW { set { SetProperty(ref _LLDetectionFDW, value); } get => GetValue(_LLDetectionFDW); }
        private float _LLDetectionFDW;

        [Category("参数DW判断配置"), DefaultValue(1.8), DisplayName("DW上限检测阈值百分比")]
        public float HLDetectionPDW { set { SetProperty(ref _HLDetectionPDW, value); } get => GetValue(_HLDetectionPDW); }
        private float _HLDetectionPDW;

        [Category("参数DW判断配置"), DefaultValue(float.MaxValue), DisplayName("DW上限检测阈值固定值")]
        public float HLDetectionFDW { set { SetProperty(ref _HLDetectionFDW, value); } get => GetValue(_HLDetectionFDW); }
        private float _HLDetectionFDW;

        [Category("参数亮度均匀性判断配置"), DefaultValue(false), DisplayName("亮度均匀性启用检测")]
        public bool IsBUEnable { set { SetProperty(ref _IsBUEnable, value); } get => GetValue(_IsBUEnable); }
        private bool _IsBUEnable;
        [Category("参数亮度均匀性判断配置"), DefaultValue(0.2), DisplayName("亮度均匀性下限检测阈值百分比")]
        public float LLDetectionPBU { set { SetProperty(ref _LLDetectionPBU, value); } get => GetValue(_LLDetectionPBU); }
        private float _LLDetectionPBU;

        [Category("参数亮度均匀性判断配置"), DefaultValue(0), DisplayName("亮度均匀性下限检测阈值固定值")]
        public float LLDetectionFBU { set { SetProperty(ref _LLDetectionFBU, value); } get => GetValue(_LLDetectionFBU); }
        private float _LLDetectionFBU;
        [Category("参数亮度均匀性判断配置"), DefaultValue(1.8), DisplayName("亮度均匀性上限检测阈值百分比")]
        public float HLDetectionPBU { set { SetProperty(ref _HLDetectionPBU, value); } get => GetValue(_HLDetectionPBU); }
        private float _HLDetectionPBU;

        [Category("参数亮度均匀性判断配置"), DefaultValue(float.MaxValue), DisplayName("亮度均匀性上限检测阈值固定值")]
        public float HLDetectionFBU { set { SetProperty(ref _HLDetectionFBU, value); } get => GetValue(_HLDetectionFBU); }
        private float _HLDetectionFBU;

        [Category("参数色度均匀性判断配置"), DefaultValue(false), DisplayName("色度均匀性启用检测")]
        public bool IsCUEnable { set { SetProperty(ref _IsCUEnable, value); } get => GetValue(_IsCUEnable); }
        private bool _IsCUEnable;

        [Category("参数色度均匀性判断配置"), DefaultValue(0.2), DisplayName("色度均匀性下限检测阈值百分比")]
        public float LLDetectionPCU { set { SetProperty(ref _LLDetectionPCU, value); } get => GetValue(_LLDetectionPCU); }
        private float _LLDetectionPCU;

        [Category("参数色度均匀性判断配置"), DefaultValue(0), DisplayName("色度均匀性下限检测阈值固定值")]
        public float LLDetectionFCU { set { SetProperty(ref _LLDetectionFCU, value); } get => GetValue(_LLDetectionFCU); }
        private float _LLDetectionFCU;

        [Category("参数色度均匀性判断配置"), DefaultValue(1.8), DisplayName("色度均匀性上限检测阈值百分比")]
        public float HLDetectionPCU { set { SetProperty(ref _HLDetectionPCU, value); } get => GetValue(_HLDetectionPCU); }
        private float _HLDetectionPCU;

        [Category("参数色度均匀性判断配置"), DefaultValue(float.MaxValue), DisplayName("色度均匀性上限检测阈值固定值")]
        public float HLDetectionFCU { set { SetProperty(ref _HLDetectionFCU, value); } get => GetValue(_HLDetectionFCU); }
        private float _HLDetectionFCU;

        [Category("检测判断配置"), DisplayName("判断方式"), Description("1-仅通过百分比阈值判断\r\n2-仅通过固定阈值判断\r\n3-结合两种方式同时判断")]
        public int JudgingWay { set { SetProperty(ref _JudgingWay, value); } get => GetValue(_JudgingWay); }
        private int _JudgingWay;

        [Category("检测判断配置"), DisplayName("多参数判断方式"), Description("1-选定参数中有一个判断NG即返回NG\r\n2-当所有选定参数都判定NG时返回NG")]
        public int JudgingWayMul { set { SetProperty(ref _JudgingWayMul, value); } get => GetValue(_JudgingWayMul); }
        private int _JudgingWayMul;
    }
}
