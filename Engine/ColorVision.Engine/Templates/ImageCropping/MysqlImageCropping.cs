using ColorVision.Database;

namespace ColorVision.Engine.Templates.ImageCropping
{
    public class MysqlImageCropping : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复MysqlImageCropping";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `pid`, `mod_type`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (32, 'ImageCropping', '发光区裁剪', NULL, 7, '2024-09-26 16:32:41', 1, 0, NULL, 0);\r\n";
            string t_scgd_sys_dictionary_mod_item = "INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4320, 'UnEgde', 4320, 'UnEgde', 0, NULL, '1', 32, '2023-11-14 17:44:15', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4321, 'O_Index', 4321, 'O_Index', 3, NULL, '[0,1,2,3]', 32, '2023-11-14 17:44:15', 1, 0, NULL);\r\n";
            return t_scgd_sys_dictionary_mod_master + t_scgd_sys_dictionary_mod_item;
        }
    }
}
