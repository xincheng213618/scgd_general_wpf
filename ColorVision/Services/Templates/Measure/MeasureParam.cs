using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Dao;
using ColorVision.Settings;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Services.Templates.Measure
{

    public class MeasureParamMenuItem : IMenuItem
    {
        public string OwnerGuid => "Template";

        public string? GuidId => "MeasureParam";
        public int Order => 31;
        public string? Header => "测量模板设置";

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
            new WindowTemplate(TemplateType.MeasureParam) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }


    public class MeasureParam : ParamBase
    {
        public MeasureParam() { }
        public MeasureParam(MeasureMasterModel dbModel)
        {
            Id = dbModel.Id;
            IsEnable = dbModel.IsEnable;
        }
    }
}
