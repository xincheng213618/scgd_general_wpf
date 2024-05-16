using ColorVision.Common.MVVM;
using ColorVision.Services.Templates;
using ColorVision.Services.Validate.Dao;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace ColorVision.Services.Validate
{

    public class ValidateParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<ValidateParam>> Params { get; set; } = new ObservableCollection<TemplateModel<ValidateParam>>();

        public ValidateParam()
        {

        }

        public List<ValidateTemplateDetailModel> DetailModels { get; set; } = new List<ValidateTemplateDetailModel>();
        private Dictionary<string, ValidateTemplateDetailModel> parameters = new Dictionary<string, ValidateTemplateDetailModel>();

        public ValidateParam(ValidateTemplateMasterModel modMaster, List<ValidateTemplateDetailModel> modDetails)
        {
            Id = modMaster.Id;
            DetailModels = modDetails;
            ValidateSingles = new ObservableCollection<ValidateSingle>();
            foreach (var DetailModel in modDetails)
            {
                if (DetailModel.Code != null)
                {
                    if (!parameters.ContainsKey(DetailModel.Code))
                    {
                        parameters.Add(DetailModel.Code, DetailModel);
                        ValidateSingles.Add(new ValidateSingle(DetailModel));
                    }
                }
            }

            if (parameters.TryGetValue("x",out var x))
                X = new ValidateSingle(x);
            if (parameters.TryGetValue("y", out var y))
                Y = new ValidateSingle(y);
            if (parameters.TryGetValue("u", out var u))
                U = new ValidateSingle(u);
            if (parameters.TryGetValue("v", out var v))
                V = new ValidateSingle(v);
            if (parameters.TryGetValue("lv", out var lv))
                Lv = new ValidateSingle(lv);
        }

        public ObservableCollection<ValidateSingle> ValidateSingles { get; set; }


        public ValidateSingle X { get; set; }
        public ValidateSingle Y { get; set; }
        public ValidateSingle U { get; set; }
        public ValidateSingle V { get; set; }
        public ValidateSingle Lv { get; set; }

    }


    public class ValidateSingle : ViewModelBase
    {
        public ValidateTemplateDetailModel Model { get; private set; }

        public ValidateSingle(ValidateTemplateDetailModel modDetails)
        {
            Model = modDetails;
        }

        public float ValMax { get => Model.ValMax; set { Model.ValMax = value; NotifyPropertyChanged(); } }
        public float ValMin { get => Model.ValMin; set { Model.ValMin = value; NotifyPropertyChanged(); } }
        public string? ValEqual { get => Model.ValEqual; set { Model.ValEqual = value; NotifyPropertyChanged(); } }
        public short ValRadix { get => Model.ValRadix; set { Model.ValRadix = value; NotifyPropertyChanged(); } }
        public short ValType { get => Model.ValType; set { Model.ValType = value; NotifyPropertyChanged(); } }
    }
}
