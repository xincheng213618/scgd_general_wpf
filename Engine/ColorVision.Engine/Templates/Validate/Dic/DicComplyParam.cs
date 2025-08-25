using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Validate
{
    public class DicComplyParam : ParamModBase
    {

        public SysDictionaryModModel ModMasterModel { get; set; }

        public DicComplyParam()
        {
        }

        public DicComplyParam(SysDictionaryModModel modMasterModel, List<SysDictionaryModItemValidateModel> dicModParams)
        {
            Id = modMasterModel.Id;
            Name = modMasterModel.Name ?? "default";
            ModMasterModel = modMasterModel;
            ModDetaiModels = new ObservableCollection<SysDictionaryModItemValidateModel>(dicModParams);
        }

        public ObservableCollection<SysDictionaryModItemValidateModel> ModDetaiModels { get; set; }
    };


}
