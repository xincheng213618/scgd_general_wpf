﻿using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Templates.POI.Comply.Dic
{
    public class ExportDicComply : MenuItemBase
    {
        public override string OwnerGuid => "Comply";

        public override string GuidId => "ComplyEdit";
        public override int Order => 99;
        public override string Header => "编辑默认合规模板";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(new TemplateDicComply()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }


}
