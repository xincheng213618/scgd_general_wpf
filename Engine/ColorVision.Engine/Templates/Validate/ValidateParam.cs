using ColorVision.Common.MVVM;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Validate
{

    public class ValidateParam : ParamModBase
    {
        public ValidateParam()
        {

        }

        public List<ValidateTemplateDetailModel> DetailModels { get; set; } = new List<ValidateTemplateDetailModel>();
        private Dictionary<string, ValidateTemplateDetailModel> parameters = new Dictionary<string, ValidateTemplateDetailModel>();

        public ValidateParam(ValidateTemplateMasterModel modMaster, List<ValidateTemplateDetailModel> modDetails)
        {
            Id = modMaster.Id;
            Name = modMaster.Name;
            DetailModels = modDetails;
            ValidateSingles = new ObservableCollection<ValidateSingle>();
            foreach (var DetailModel in modDetails)
            {
                if (DetailModel.Code != null)
                {
                    if (parameters.TryAdd(DetailModel.Code, DetailModel))
                        ValidateSingles.Add(new ValidateSingle(DetailModel));
                }
            }
        }

        public ObservableCollection<ValidateSingle> ValidateSingles { get; set; }
    }


    public class ValidateSingle : ViewModelBase
    {
        public ValidateTemplateDetailModel Model { get; private set; }

        public ValidateSingle(ValidateTemplateDetailModel modDetails)
        {
            Model = modDetails;
        }

        public float ValMax { get => Model.ValMax; set { Model.ValMax = value; OnPropertyChanged(); } }
        public float ValMin { get => Model.ValMin; set { Model.ValMin = value; OnPropertyChanged(); } }
        public string ValEqual { get => Model.ValEqual; set { Model.ValEqual = value; OnPropertyChanged(); } }
        public short ValRadix { get => Model.ValRadix; set { Model.ValRadix = value; OnPropertyChanged(); } }
        public short ValType { get => Model.ValType; set { Model.ValType = value; OnPropertyChanged(); } }
    }
}
