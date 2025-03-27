using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Solution.Searches;
using ColorVision.UI.Menus;
using ColorVision.UI.PropertyEditor;
using System.Windows;

namespace ColorVision.Engine.DataHistory.Dao
{
    public class MenuArchive : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;

        public override string GuidId => nameof(MenuArchive);

        public override string Header => "归档";
    }

    public class MenuArchiveManager : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuArchive);


        public override string Header => "归档管理";

        public override void Execute()
        {
            SolutionView solutionView = new SolutionView();
            Window window = new Window();
            window.Content = solutionView;
            window.Show();
        }
    }

    public class MenuGlobleCfg : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuArchive);

        public override string Header => "归档服务器配置";

        public override void Execute()
        {
            GlobleCfgdModel globleCfgdModel = GlobleCfgdDao.Instance.GetArchDB();
            if (globleCfgdModel == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到归档服务器配置，正在重置", "ColorVision");
                string sql = "INSERT INTO `cv`.`t_scgd_sys_globle_cfg` (`id`, `code`, `name`, `cfg_type`, `cfg_value`, `is_deleted`, `is_enabled`, `remark`, `tenant_id`) VALUES (3, 'arch_db', '归档服务数据库', 10, '{\\\"Name\\\":null,\\\"Host\\\":\\\"localhost\\\",\\\"Port\\\":3306,\\\"UserName\\\":\\\"cv\\\",\\\"UserPwd\\\":\\\"9p9DMdywXwaTbAXt0oJkUnAb\\\",\\\"Database\\\":\\\"color_vision_arch_2025\\\"}', 0, 1, NULL, 0);\r\n";
                MySqlControl.GetInstance().ExecuteNonQuery(sql);
                globleCfgdModel = GlobleCfgdDao.Instance.GetArchDB();
            }

            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(globleCfgdModel, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            propertyEditorWindow.Submited += (s, e) => { GlobleCfgdDao.Instance.Save(globleCfgdModel); };
            propertyEditorWindow.ShowDialog();
        }
    }


    public class MenuConfigArchive : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuArchive);

        public override string Header => "归档配置";

        public override void Execute()
        {
            string sql = "ALTER TABLE `t_scgd_sys_config_archived` ADD COLUMN `excluding_images` TINYINT(1) NOT NULL DEFAULT '0' AFTER `data_save_days`;  ALTER TABLE `t_scgd_sys_config_archived` ADD COLUMN `del_local_file` tinyint(1) NOT NULL DEFAULT '0';  ALTER TABLE `t_scgd_sys_config_archived` ADD COLUMN `data_save_hours` int(11) NOT NULL DEFAULT '0';";
            MySqlControl.GetInstance().ExecuteNonQuery(sql);
            ConfigArchivedModel configArchivedModel = ConfigArchivedDao.Instance.GetById(1);
            if (configArchivedModel == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到归档配置信息", "ColorVision");
                return;
            }
            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(configArchivedModel, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            propertyEditorWindow.Submited += (s, e) => { ConfigArchivedDao.Instance.Save(configArchivedModel); };
            propertyEditorWindow.ShowDialog();
        }
    }
}
