using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus
{
    public class TemplateAutoFocus : ITemplate<AutoFocusParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<AutoFocusParam>> Params { get; set; } = new ObservableCollection<TemplateModel<AutoFocusParam>>();

        public TemplateAutoFocus()
        {
            Title = "自动聚焦模板设置";
            Code = "AutoFocus";
            TemplateParams = Params;
        }

        public override IMysqlCommand? GetMysqlCommand() => new MysqAutoFocus();

        public override void Save()
        {
            //if (SaveIndex.Count == 0) return;
            //foreach (var index in SaveIndex)
            //{
            //    if (index > -1 && index < TemplateParams.Count)
            //    {
            //        var item = TemplateParams[index];

            //        if (item.Value.MaxPosition % item.Value.CurStep != 0)
            //        {
            //            int value = ((int)(item.Value.MaxPosition / item.Value.CurStep)) * item.Value.CurStep;
            //            if (MessageBox.Show($"超出步长限制，是否自动调整MaxPosition{item.Value.MaxPosition}到{value}", "ColorVsiion", MessageBoxButton.YesNo)== MessageBoxResult.Yes)
            //            {
            //                item.Value.MaxPosition = value;
            //            }

            //        }
            //    }
            //}
            base.Save();
        }
    }
}
