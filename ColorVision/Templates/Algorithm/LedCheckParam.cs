#pragma warning disable CA1707,IDE1006

using ColorVision.MySql.DAO;
using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace ColorVision.Templates.Algorithm
{
    public class LedCheckParam : ParamBase
    {
        public LedCheckParam() { }
        public LedCheckParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name??string.Empty, modDetails)
        {
        }

        [Category("LedCheck"), Description("checkChannel")]
        [JsonProperty("checkChannel")]
        public int CheckChannel { get => GetValue(_CheckChannel); set { SetProperty(ref _CheckChannel, value); } }
        private int _CheckChannel;


        [Category("LedCheck"), Description("isguding")]
        [JsonProperty("isguding")]
        public int Isguding { get => GetValue(_Isguding); set { SetProperty(ref _Isguding, value); } }
        private int _Isguding;

        [Category("LedCheck"), Description("gudingrid")]
        [JsonProperty("gudingrid")]
        public int Gudingrid { get => GetValue(_Gudingrid); set { SetProperty(ref _Gudingrid, value); } }
        private int _Gudingrid;

        [Category("LedCheck"), Description("lunkuomianji")]
        [JsonProperty("lunkuomianji")]
        public int Lunkuomianji { get => GetValue(_Lunkuomianji); set { SetProperty(ref _Lunkuomianji, value); } }
        private int _Lunkuomianji;

        [Category("LedCheck"), Description("pointNum")]
        [JsonProperty("pointNum")]
        public int PointNum { get => GetValue(_PointNum); set { SetProperty(ref _PointNum, value); } }
        private int _PointNum;

        [Category("LedCheck"), Description("hegexishu")]
        [JsonProperty("hegexishu")]
        public double Hegexishu { get => GetValue(_Hegexishu); set { SetProperty(ref _Hegexishu, value); } }
        private double _Hegexishu;

        [Category("LedCheck"), Description("erzhihuapiancha")]
        [JsonProperty("erzhihuapiancha")]
        public int Erzhihuapiancha { get => GetValue(_Erzhihuapiancha); set { SetProperty(ref _Erzhihuapiancha, value); } }
        private int _Erzhihuapiancha;

        [Category("LedCheck"), Description("binaryCorret")]
        [JsonProperty("binaryCorret")]
        public int BinaryCorret { get => GetValue(_BinaryCorret); set { SetProperty(ref _BinaryCorret, value); } }
        private int _BinaryCorret;


        [Category("LedCheck"), Description("boundry")]
        [JsonProperty("boundry")]
        public int Boundry { get => GetValue(_Boundry); set { SetProperty(ref _Boundry, value); } }
        private int _Boundry;


        [Category("LedCheck"), Description("isuseLocalRdPoint")]
        [JsonProperty("isuseLocalRdPoint")]
        public int IsuseLocalRdPoint { get => GetValue(_IsuseLocalRdPoint); set { SetProperty(ref _IsuseLocalRdPoint, value); } }
        private int _IsuseLocalRdPoint;

        [Category("LedCheck"), Description("picwid")]
        [JsonProperty("picwid")]
        public int Picwid { get => GetValue(_Picwid); set { SetProperty(ref _Picwid, value); } }
        private int _Picwid;

        [Category("LedCheck"), Description("pichig")]
        [JsonProperty("pichig")]
        public int Pichig { get => GetValue(_Pichig); set { SetProperty(ref _Pichig, value); } }
        private int _Pichig;

        [Category("LedCheck"), Description("LengthCheck")]
        [JsonProperty("pichig")]
        public double[] LengthCheck { get => GetValue(_LengthCheck); set { SetProperty(ref _LengthCheck, value); } }
        private double[] _LengthCheck;

        [Category("LedCheck"), Description("LengthRange")]
        [JsonProperty("LengthRange")]
        public double[] LengthRange { get => GetValue(_LengthRange); set { SetProperty(ref _LengthRange, value); } }
        private double[] _LengthRange;

        [Category("LedCheck"), Description("localRdMark")]
        [JsonProperty("localRdMark")]
        public double[] LocalRdMark { get => GetValue(_LocalRdMark); set { SetProperty(ref _LocalRdMark, value); } }
        private double[] _LocalRdMark;

    }
}
