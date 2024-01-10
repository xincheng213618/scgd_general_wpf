using ColorVision.MySql.DAO;
using ColorVision.Templates;
using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Services.Device.Algorithm.Templates
{
    public class SFRParam : ParamBase
    {
        public SFRParam() { }
        public SFRParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
        {
        }

        [Category("SFR"), Description("Gamma")]
        public double Gamma { get => GetValue(_Gamma); set { SetProperty(ref _Gamma, value); } }
        private double _Gamma = 0.01;

        [Category("SFR"), Description("ROI x"), DisplayName("ROI X")]

        public int X { get => GetValue(_X); set { SetProperty(ref _X, value); } }
        private int _X;
        [Category("SFR"), Description("ROI y"), DisplayName("ROI Y")]
        public int Y { get => GetValue(_Y); set { SetProperty(ref _Y, value); } }
        private int _Y;
        [Category("SFR"), Description("ROI Width"), DisplayName("ROI Width")]
        public int Width { get => GetValue(_Width); set { SetProperty(ref _Width, value); } }
        private int _Width = 1000;
        [Category("SFR"), Description("ROI Height"), DisplayName("ROI Height")]
        public int Height { get => GetValue(_Height); set { SetProperty(ref _Height, value); } }
        private int _Height = 1000;

        [Category("SFR"), Description("ROI"), Browsable(false)]
        public CRECT ROI { get => new CRECT() { x = X, y = Y, cx = Width, cy = Height }; }
    }
}
