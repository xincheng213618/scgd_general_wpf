using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.CADMapping
{
    public class CADMappingParam : ParamBase
    {
        public CADMappingParam()
        {

        }

        public CADMappingParam(ModMasterModel modMaster, List<ModDetailModel> aoiDetail) : base(modMaster.Id, modMaster.Name ?? string.Empty, aoiDetail)
        {

        }

        public int MarginLeft { get => GetValue(_MarginLeft); set { SetProperty(ref _MarginLeft, value); NotifyPropertyChanged(); } }
        private int _MarginLeft =33;
        public int MarginTop { get => GetValue(_MarginTop); set { SetProperty(ref _MarginTop, value); NotifyPropertyChanged(); } }
        private int _MarginTop = 33;
        public int MarginRight { get => GetValue(_MarginRight); set { SetProperty(ref _MarginRight, value); NotifyPropertyChanged(); } }
        private int _MarginRight = 33;
        public int MarginBottom { get => GetValue(_MarginBottom); set { SetProperty(ref _MarginBottom, value); NotifyPropertyChanged(); } }
        private int _MarginBottom = 33;

        public BorderType MarginType { get => GetValue(_MarginType); set { SetProperty(ref _MarginType, value); NotifyPropertyChanged(); } }
        private BorderType _MarginType = BorderType.Relative;
    }


}
