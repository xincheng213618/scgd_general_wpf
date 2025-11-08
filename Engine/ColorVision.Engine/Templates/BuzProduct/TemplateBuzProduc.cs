using ColorVision.Database;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.BuzProduct
{
    public class TemplateBuzProduc: ITemplateBuzProduc<TemplateBuzProductParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateBuzProductParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateBuzProductParam>>();

        public TemplateBuzProduc()
        {
            Title = ColorVision.Engine.Properties.Resources.BuzProcucTemplateManagement;
            Code = "BuzProduc";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditTemplate.SetParam(TemplateParams[index].Value);
        }
        public EditTemplateBuzProduct EditTemplate { get; set; } = new EditTemplateBuzProduct();

        public override UserControl GetUserControl()
        {
            return EditTemplate;
        }

        public override UserControl CreateUserControl() => new EditTemplateBuzProduct();

        public override IMysqlCommand? GetMysqlCommand()
        {
            return new MysqlBuzProduct();
        }
    }

}
