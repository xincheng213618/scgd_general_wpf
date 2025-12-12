using System.Collections.Generic;

namespace ColorVision.Engine.Templates.ARVR.AOI
{
    public class AOIParam : ParamModBase
    {
        public AOIParam() { }
        public AOIParam(ModMasterModel modMaster, List<ModDetailModel> sxDetail) : base(modMaster, sxDetail) { }

        public int Left
        {
            set { SetProperty(ref _Left, value); }
            get => GetValue(_Left);
        }
        private int _Left = 5;

        public int Right
        {
            set { SetProperty(ref _Right, value); }
            get => GetValue(_Right);
        }
        private int _Right = 5;

        public int Top
        {
            set { SetProperty(ref _Top, value); }
            get => GetValue(_Top);
        }
        private int _Top = 5;

        public int Bottom
        {
            set { SetProperty(ref _Bottom, value); }
            get => GetValue(_Bottom);
        }
        private int _Bottom = 5;

        public int BlurSize
        {
            set { SetProperty(ref _BlurSize, value); }
            get => GetValue(_BlurSize);
        }
        private int _BlurSize = 19;

        public int DilateSize
        {
            set { SetProperty(ref _DilateSize, value); }
            get => GetValue(_DilateSize);
        }
        private int _DilateSize = 5;

        public bool FilterByContrast
        {
            set { SetProperty(ref _FilterByContrast, value); }
            get => GetValue(_FilterByContrast);
        }
        private bool _FilterByContrast = true;

        public double MaxContrast
        {
            set { SetProperty(ref _MaxContrast, value); }
            get => GetValue(_MaxContrast);
        }
        private double _MaxContrast = 1.7;

        public double MinContrast
        {
            set { SetProperty(ref _MinContrast, value); }
            get => GetValue(_MinContrast);
        }
        private double _MinContrast = 0.3;
    }
}
