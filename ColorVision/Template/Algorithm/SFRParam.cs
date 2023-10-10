#pragma warning disable CA1707
using ColorVision.MySql.DAO;
using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Template.Algorithm
{
    public class SFRParam : ParamBase
    {
        public SFRParam() { }
        public SFRParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modDetails)
        {
        }

        [Category("SFR"), Description("SFR gamma")]
        public double SFR_gamma { get => GetValue(_SFR_gamma); set { SetProperty(ref _SFR_gamma, value); } }
        private double _SFR_gamma = 0.01;

        [Category("SFR"), Description("ROI x")]

        public int X { get => GetValue(_X); set { SetProperty(ref _X, value); } }
        private int _X = 0;
        [Category("SFR"), Description("ROI y")]
        public int Y { get => GetValue(_Y); set { SetProperty(ref _Y, value); } }
        private int _Y = 0;
        [Category("SFR"), Description("ROI Width")]
        public int Width { get => GetValue(_Width); set { SetProperty(ref _Width, value); } }
        private int _Width = 1000;
        [Category("SFR"), Description("ROI Height")]
        public int Height { get => GetValue(_Height); set { SetProperty(ref _Height, value); } }
        private int _Height = 1000;

        [Category("SFR"), Description("ROI")]
        public CRECT ROI { get => new CRECT() { x = X, y = Y, cx = Width, cy = Height }; }
    }
}
