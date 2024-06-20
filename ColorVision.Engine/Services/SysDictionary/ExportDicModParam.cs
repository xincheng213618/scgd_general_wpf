using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Services.SysDictionary
{
    public class ExportDicModParam : IMenuItem
    {
        public string OwnerGuid => "Template";

        public string? GuidId => "DicModParam";
        public int Order => 31;
        public string? Header => "模板字典表";
        public Visibility Visibility => Visibility.Visible;
        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(a =>
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(new TemplateDicModParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }
}
