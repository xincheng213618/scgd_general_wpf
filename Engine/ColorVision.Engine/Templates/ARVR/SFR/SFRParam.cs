using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Templates.SFR
{

    public class SFRParam : ParamModBase
    {
        public SFRParam()
        {

        }
        public SFRParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        [Category("SFR"), Description("Gamma")]
        public double Gamma { get => GetValue(_Gamma); set { SetProperty(ref _Gamma, value); } }
        private double _Gamma = 0.01;

        [Category("SFR"), Description("ROI x"), DisplayName("ROI X")]

        public int X { get => GetValue(_X); set { SetProperty(ref _X, value); OnPropertyChanged(nameof(RECT)); } }
        private int _X;

        [Category("SFR"), Description("ROI y"), DisplayName("ROI Y")]
        public int Y { get => GetValue(_Y); set { SetProperty(ref _Y, value); OnPropertyChanged(nameof(RECT)); } }
        private int _Y;
        [Category("SFR"), Description("ROI Width"), DisplayName("ROI Width")]
        public int Width { get => GetValue(_Width); set { SetProperty(ref _Width, value); OnPropertyChanged(nameof(RECT)); } }
        private int _Width = 100;
        [Category("SFR"), Description("ROI Height"), DisplayName("ROI Height")]
        public int Height { get => GetValue(_Height); set { SetProperty(ref _Height, value); OnPropertyChanged(nameof(RECT)); } }
        private int _Height = 100;

        [Category("SFR"), Description("ROI"), Browsable(false)]
        public Rect RECT { get => new Rect() { X =X , Y = Y, Width = Width, Height = Height }; set { X = (int)value.X; Y = (int)value.Y; Width = (int)value.Width; Height = (int)value.Height; OnPropertyChanged(); } }

    }
}
