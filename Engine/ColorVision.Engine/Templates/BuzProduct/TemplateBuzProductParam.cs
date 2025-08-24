using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.BuzProduct
{

    

    public class TemplateBuzProductParam : ParamBase
    {
        [Browsable(false)]
        [JsonIgnore]
        public BuzProductMasterModel BuzProductMasterModel { get; set; }

        public RelayCommand CreateBuzProductDetailCommamd { get; set; }

        public TemplateBuzProductParam()
        {
            BuzProductMasterModel = new BuzProductMasterModel();
            CreateBuzProductDetailCommamd = new RelayCommand(a => CreateBuzProductDetail());


        }

        public TemplateBuzProductParam(BuzProductMasterModel templateJsonModel)
        {
            BuzProductMasterModel = templateJsonModel;
            CreateBuzProductDetailCommamd = new RelayCommand(a => CreateBuzProductDetail());
        }

        public override int Id { get => BuzProductMasterModel.Id; set { BuzProductMasterModel.Id = value; OnPropertyChanged(); } }
        public override string Name { get => BuzProductMasterModel.Name ?? string.Empty; set { BuzProductMasterModel.Name = value; OnPropertyChanged(); } }

        public ObservableCollection<BuzProductDetailModel> BuzProductDetailModels { get; set; } = new ObservableCollection<BuzProductDetailModel>();

        public void CreateBuzProductDetail()
        {
            BuzProductDetailModel buzProductDetailModel = new BuzProductDetailModel();
            buzProductDetailModel.Pid = BuzProductMasterModel.Id;
            buzProductDetailModel.Code =string.Empty;
            BuzProductDetailDao.Instance.Save(buzProductDetailModel);
            BuzProductDetailModels.Add(buzProductDetailModel);
        }
    }
}
