#pragma warning disable CS8603
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates.POI;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.LargeFlow
{



    public class TemplateLargeFlow : ITemplateJson<TJLargeFlowParam>,IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TJLargeFlowParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TJLargeFlowParam>>();

        public TemplateLargeFlow()
        {
            Title = "大模板模板管理";
            Code = "LargeFlow";
            TemplateDicId = 999;
            TemplateParams = Params;
            IsSideHide = true;
        }
        public EditLargeFlow EditWindow { get; set; }  
        public override void PreviewMouseDoubleClick(int index)
        {
            EditWindow = new EditLargeFlow(Params[index].Value) { Owner = Application.Current.GetActiveWindow() };
            EditWindow.ShowDialog();
        }

        public override IMysqlCommand? GetMysqlCommand() => new MysqlLargeFlow();


    }




}
