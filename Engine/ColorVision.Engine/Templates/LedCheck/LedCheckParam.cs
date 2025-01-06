using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.LedCheck
{

    public class LedCheckParam : ParamModBase
    {

        public LedCheckParam() { }
        public LedCheckParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        [Category("LedCheck"), DisplayName("灯珠抓取通道"), Description("灯珠抓取通道 checkChannel")]
        [JsonProperty("checkChannel")]
        public int CheckChannel { get => GetValue(_CheckChannel); set { SetProperty(ref _CheckChannel, value); } }
        private int _CheckChannel = 1;


        [Category("LedCheck"), DisplayName("是否启用固定半径计算"), Description("是否启用固定半径计算 isguding")]
        [JsonProperty("isguding")]
        public int Isguding { get => GetValue(_Isguding); set { SetProperty(ref _Isguding, value); } }
        private int _Isguding = 2;

        [Category("LedCheck"), DisplayName("灯珠固定半径"), Description("灯珠固定半径 gudingrid")]
        [JsonProperty("gudingrid")]
        public int Gudingrid { get => GetValue(_Gudingrid); set { SetProperty(ref _Gudingrid, value); } }
        private int _Gudingrid = 15;

        [Category("LedCheck"), DisplayName("轮廓最小面积"), Description("轮廓最小面积  lunkuomianji")]
        [JsonProperty("lunkuomianji")]
        public int Lunkuomianji { get => GetValue(_Lunkuomianji); set { SetProperty(ref _Lunkuomianji, value); } }
        private int _Lunkuomianji = 5;



        [Category("LedCheck"), DisplayName("轮廓范围系数"), Description("轮廓范围系数 hegexishu")]
        [JsonProperty("hegexishu")]
        public double Hegexishu { get => GetValue(_Hegexishu); set { SetProperty(ref _Hegexishu, value); } }
        private double _Hegexishu = 0.3;

        [Category("LedCheck"), DisplayName("图像二值化补正"), Description("图像二值化补正 erzhihuapiancha")]
        [JsonProperty("erzhihuapiancha")]
        public int Erzhihuapiancha { get => GetValue(_Erzhihuapiancha); set { SetProperty(ref _Erzhihuapiancha, value); } }
        private int _Erzhihuapiancha = -20;

        [Category("LedCheck"), DisplayName("发光区二值化补正"), Description("发光区二值化补正 binaryCorret")]
        [JsonProperty("binaryCorret")]
        public int BinaryCorret { get => GetValue(_BinaryCorret); set { SetProperty(ref _BinaryCorret, value); } }
        private int _BinaryCorret = -20;


        [Category("LedCheck"), DisplayName("boundry"), Description("boundry")]
        [JsonProperty("boundry")]
        public int Boundry { get => GetValue(_Boundry); set { SetProperty(ref _Boundry, value); } }
        private int _Boundry = 120;


        [Category("LedCheck"), DisplayName("是否使用本地点位信息计算"), Description("是否使用本地点位信息计算 isuseLocalRdPoint")]
        [JsonProperty("isuseLocalRdPoint")]
        public bool IsuseLocalRdPoint { get => GetValue(_IsuseLocalRdPoint); set { SetProperty(ref _IsuseLocalRdPoint, value); } }
        private bool _IsuseLocalRdPoint;

        [Category("LedCheck"), DisplayName("灯珠宽方向数量"), Description("灯珠宽方向数量 picwid")]
        [JsonProperty("picwid")]
        public int Picwid { get => GetValue(_Picwid); set { SetProperty(ref _Picwid, value); NotifyPropertyChanged(nameof(PointNum)); } }
        private int _Picwid = 32;

        [Category("LedCheck"), DisplayName("灯珠高方向数量"), Description("灯珠高方向数量 pichig")]
        [JsonProperty("pichig")]
        public int Pichig { get => GetValue(_Pichig); set { SetProperty(ref _Pichig, value); NotifyPropertyChanged(nameof(PointNum)); } }
        private int _Pichig = 24;

        [Browsable(false),]
        [JsonProperty("pointNum")]
        public int PointNum { get => _Picwid * Pichig; }

        [Category("LedCheck"), Description("LengthCheck")]
        public double[]? LengthCheck { get => GetValue(_LengthCheck); set { SetProperty(ref _LengthCheck, value); } }
        private double[]? _LengthCheck = new double[] { 10, 10, 10, 10 };

        [Category("LedCheck"), Description("LengthRange")]
        public double[]? LengthRange { get => GetValue(_LengthRange); set { SetProperty(ref _LengthRange, value); } }
        private double[]? _LengthRange = new double[] { 10, 10, 10, 10 };

        [Category("LedCheck"), Description("localRdMark")]
        [JsonProperty("localRdMark")]
        public double[]? LocalRdMark { get => GetValue(_LocalRdMark); set { SetProperty(ref _LocalRdMark, value); } }
        private double[]? _LocalRdMark = new double[] { 10, 10, 10, 10 };

    }
}
