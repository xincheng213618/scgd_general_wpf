using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.FocusPoints
{
    public class FocusPointsParam : ParamModBase
    {
        public FocusPointsParam() { }

        public FocusPointsParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }
        [Category("Binarize"), Description("二值化")]
        public bool Binarize { get => GetValue(_Binarize); set { SetProperty(ref _Binarize, value); } }
        private bool _Binarize;

        [Category("Binarize"), Description("二值化阈值")]
        public int BinarizeThresh { get => GetValue(_BinarizeThresh); set { SetProperty(ref _BinarizeThresh, value); } }
        private int _BinarizeThresh;

        [Category("Blur"), Description("均值滤波")]
        public bool Blur { get => GetValue(_Blur); set { SetProperty(ref _Blur, value); } }
        private bool _Blur;

        [Category("Blur"), Description("均值滤波值")]
        public int BlurSize { get => GetValue(_BlurSize); set { SetProperty(ref _BlurSize, value); } }
        private int _BlurSize;


        [Category("Erode"), Description("腐蚀")]
        public bool Erode { get => GetValue(_Erode); set { SetProperty(ref _Erode, value); } }
        private bool _Erode;

        [Category("Erode"), Description("腐蚀值")]
        public int ErodeSize { get => GetValue(_ErodeSize); set { SetProperty(ref _ErodeSize, value); } }
        private int _ErodeSize;


        [Category("Dilate"), Description("膨胀")]
        public bool Dilate { get => GetValue(_Dilate); set { SetProperty(ref _Dilate, value); } }
        private bool _Dilate;

        [Category("Dilate"), Description("腐蚀值")]
        public int DilateSize { get => GetValue(_DilateSize); set { SetProperty(ref _DilateSize, value); } }
        private int _DilateSize;

        [Category("Param"), Description("矩形过滤")]
        public bool FilterRect { get => GetValue(_FilterRect); set { SetProperty(ref _FilterRect, value); } }
        private bool _FilterRect;

        [Category("Param"), Description("宽度")]
        public int Width { get => GetValue(_Width); set { SetProperty(ref _Width, value); } }
        private int _Width = 100;

        [Category("Param"), Description("高度")]
        public int Height { get => GetValue(_Height); set { SetProperty(ref _Height, value); } }
        private int _Height = 100;

        [Category("FilterArea"), Description("区域过滤")]
        public bool FilterArea { get => GetValue(_FilterArea); set { SetProperty(ref _FilterArea, value); } }
        private bool _FilterArea;



        [Category("FilterArea"), Description("最大面积")]
        public int MaxArea { get => GetValue(_MaxArea); set { SetProperty(ref _MaxArea, value); } }
        private int _MaxArea = 100;

        [Category("FilterArea"), Description("最小面积")]
        public int MinArea { get => GetValue(_MinArea); set { SetProperty(ref _MinArea, value); } }
        private int _MinArea = 100;


        [Category("Roi"), Description("Roi")]
        public bool Roi { get => GetValue(_Roi); set { SetProperty(ref _Roi, value); } }
        private bool _Roi;

        [Category("Roi"), Description("左")]
        public int Left { get => GetValue(_Left); set { SetProperty(ref _Left, value); } }
        private int _Left = 100;

        [Category("Roi"), Description("右")]
        public int Right { get => GetValue(_Right); set { SetProperty(ref _Right, value); } }
        private int _Right = 100;

        [Category("Roi"), Description("上")]
        public int Top { get => GetValue(_Top); set { SetProperty(ref _Top, value); } }
        private int _Top = 100;

        [Category("Roi"), Description("下")]
        public int Bottom { get => GetValue(_Bottom); set { SetProperty(ref _Bottom, value); } }
        private int _Bottom = 100;

    }
}
