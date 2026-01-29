using ColorVision.Database;
using log4net;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.CaliAngleShift
{

    public class TJCaliAngleShiftParam : TemplateJsonParam
    {
        private static ILog log = LogManager.GetLogger(nameof(TJCaliAngleShiftParam));



        public TJCaliAngleShiftParam() : base()
        {
        }

        public TJCaliAngleShiftParam(ModMasterModel templateJsonModel) : base(templateJsonModel)
        {

        }


    }

    public class TemplateCaliAngleShift : ITemplateJson<TJCaliAngleShiftParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJCaliAngleShiftParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJCaliAngleShiftParam>>();

        public TemplateCaliAngleShift()
        {
            Title = ColorVision.Engine.Properties.Resources.ColorCorrection;
            Code = "CaliAngleShift";
            Name = "CaliAngleShift";
            TemplateDicId = 51;
            TemplateParams = Params;
            IsUserControl = true;
        }


        public override void SetUserControlDataContext(int index)
        {
            EditTemplateJson.SetParam(TemplateParams[index].Value);
        }
        public EditTemplateJson EditTemplateJson { get; set; }

        public override UserControl GetUserControl()
        {
            EditTemplateJson = new EditTemplateJson(Description);
            return EditTemplateJson;
        }
        public string Description { get; set; } = "{\r\n  \"coeff_b\": [-2.05705963006213, -0.203402567360732, -0.00000871816081889568, 0.00000000985087345114056, -0.00000000000104340220586239, 1.38791315222617e-16, -8.29378026031862e-21],\r\n  \"coeff_g\": [-2.1051636728357, -0.204828890611705, -0.00000786286597975059, 0.00000000969936743625218, -0.00000000000110631282749306, 1.56962468799272e-16, -9.48550126449628e-21],\r\n  \"coeff_r\": [-2.10810283209893, -0.208726349137694, -0.00000959350167719098, 0.000000009837748576948453, -0.000000000000888646394378553, 9.502897498916671e-17, -4.70095868790552e-21],\r\n  \"caliType\": 15,\r\n  \"vamAngle\": 60,\r\n  \"target_col\": 6280,\r\n  \"target_row\": 4210,\r\n  \"rowColShift\": [0, 0],\r\n  \"optical_center_x\": 3140,\r\n  \"optical_center_y\": 2105,\r\n  \"coefficient_order\": 6,\r\n  \"interpolate_ratio\": 3\r\n}";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlCaliAngleShift();

    }




}
