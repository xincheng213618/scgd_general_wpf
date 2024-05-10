using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Dao;
using ColorVision.Settings;
using ColorVision.UI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Services.Templates.Measure
{

    public class ExportMeasureParam : IMenuItem
    {
        public string OwnerGuid => "Template";

        public string? GuidId => "MeasureParam";
        public int Order => 31;
        public string? Header => ColorVision.Properties.Resource.MenuMeasure;

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new RelayCommand(a =>
        {
            SoftwareConfig SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
            if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(new TemplateMeasureParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }


    public class TemplateMeasureParam : ITemplate<MeasureParam>, IITemplateLoad
    {
        public TemplateMeasureParam()
        {
            Title = "MeasureParam";
            TemplateParams = MeasureParam.Params;
        }

        public override void Load() => MeasureParam.LoadMeasureParams();

        public override void Create(string templateName) => MeasureParam.AddMeasureParam(templateName);
    }


    public class MeasureParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<MeasureParam>> Params { get; set; } = new ObservableCollection<TemplateModel<MeasureParam>>();

        public static ObservableCollection<TemplateModel<MeasureParam>> LoadMeasureParams()
        {
            Params.Clear();
            if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
            {
                List<MeasureMasterModel> devices = MeasureMasterDao.Instance.GetAllByTenantId(ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
                foreach (var dbModel in devices)
                {
                    Params.Add(new TemplateModel<MeasureParam>(dbModel.Name ?? "default", new MeasureParam(dbModel)));
                }
            }
            return Params;
        }

        public static MeasureParam? AddMeasureParam(string name)
        {
            MeasureMasterModel model = new MeasureMasterModel(name, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            MeasureMasterDao.Instance.Save(model);
            int pkId = model.Id;
            if (pkId > 0)
            {
                return LoadMeasureParamById(pkId);
            }
            return null;
        }
        public static MeasureParam? LoadMeasureParamById(int pkId)
        {
            MeasureMasterModel model = MeasureMasterDao.Instance.GetById(pkId);
            if (model != null) return new MeasureParam(model);
            else return null;
        }



        public MeasureParam() { }
        public MeasureParam(MeasureMasterModel dbModel)
        {
            Id = dbModel.Id;
            IsEnable = dbModel.IsEnable;
        }
    }
}
