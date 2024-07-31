using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Templates;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.BuildPoi;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.Manager
{
    public class ModThirdPartyManagerParam : ParamBase
    {

        public ModThirdPartyManagerParam()
        {
            ModThirdPartyAlgorithmsModel = new ThirdPartyAlgorithmsModel();
        }

        public ModThirdPartyManagerParam(ThirdPartyAlgorithmsModel modThirdPartyAlgorithmsModel)
        {
            ModThirdPartyAlgorithmsModel = modThirdPartyAlgorithmsModel;
        }

        public override int Id { get => ModThirdPartyAlgorithmsModel.Id; set { ModThirdPartyAlgorithmsModel.Id = value; NotifyPropertyChanged(); } }
        public override string Name { get => ModThirdPartyAlgorithmsModel.Name ?? string.Empty; set { ModThirdPartyAlgorithmsModel.Name = value; NotifyPropertyChanged(); } }

        public ThirdPartyAlgorithmsModel ModThirdPartyAlgorithmsModel { get; set; }
    }
}
