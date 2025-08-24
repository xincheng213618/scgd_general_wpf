using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.Manager
{
    public class ModThirdPartyManagerParam : ParamModBase
    {

        public ModThirdPartyManagerParam()
        {
            ModThirdPartyAlgorithmsModel = new ThirdPartyAlgorithmsModel();
        }

        public ModThirdPartyManagerParam(ThirdPartyAlgorithmsModel modThirdPartyAlgorithmsModel)
        {
            ModThirdPartyAlgorithmsModel = modThirdPartyAlgorithmsModel;
        }

        public override int Id { get => ModThirdPartyAlgorithmsModel.Id; set { ModThirdPartyAlgorithmsModel.Id = value; OnPropertyChanged(); } }
        public override string Name { get => ModThirdPartyAlgorithmsModel.Name ?? string.Empty; set { ModThirdPartyAlgorithmsModel.Name = value; OnPropertyChanged(); } }

        public ThirdPartyAlgorithmsModel ModThirdPartyAlgorithmsModel { get; set; }
    }
}
