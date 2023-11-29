#pragma warning disable CS8603  

using ColorVision.MySql.DAO;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Templates
{
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


        public AoiParam(ModMasterModel modMaster, List<ModDetailModel> aoiDetail) : base(modMaster.Id, modMaster.Name ?? string.Empty, aoiDetail)
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
}
