#pragma warning disable CA1707,IDE1006

using ColorVision.Engine.Templates;
using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.ImageCropping
{
    public class ImageCroppingParam : ParamBase
    {
        public ImageCroppingParam() { }
        public ImageCroppingParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        [Category("UnEgde"), Description("UnEgde")]
        public int UnEgde { get => GetValue(_UnEgde); set { SetProperty(ref _UnEgde, value); } }
        private int _UnEgde = 1;

    }
}
