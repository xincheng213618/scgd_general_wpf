#pragma warning disable CA1707,IDE1006

using ColorVision.Engine.Templates;
using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Templates.POI.POIFix
{
    public class PoiFixParam : ParamModBase
    {
        public PoiFixParam() { }
        public PoiFixParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }
        [Category("MTF"), Description("CSV")]
        public string? PoiFixFilePath { get => GetValue(_PoiFixFilePath); set { SetProperty(ref _PoiFixFilePath, value); NotifyPropertyChanged(); } }
        private string? _PoiFixFilePath ;

    }
}
