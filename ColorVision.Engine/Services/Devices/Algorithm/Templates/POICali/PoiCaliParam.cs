using ColorVision.Engine.Templates;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POICali
{

    public class PoiCaliParam : ParamBase
    {

        public PoiCaliParam()
        {
        }

        public PoiCaliParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {

        }
        [Category("PoiCaliParam"), Description("A")]
        public float A { get => GetValue(_A); set { SetProperty(ref _A, value); } }
        private float _A = 0.01f;

        [Category("PoiCaliParam"), Description("B")]
        public float B { get => GetValue(_B); set { SetProperty(ref _B, value); } }
        private float _B = 0.01f;
        [Category("PoiCaliParam"), Description("C")]
        public float C { get => GetValue(_C); set { SetProperty(ref _C, value); } }
        private float _C = 0.01f;

    }
}
