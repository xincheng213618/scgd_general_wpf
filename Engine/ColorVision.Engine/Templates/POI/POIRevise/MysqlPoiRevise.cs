using ColorVision.Database;

namespace ColorVision.Engine.Templates.POI.POIRevise
{
    public class MysqlPoiRevise : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复Mysql Poi修正模板设置";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `pid`, `mod_type`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (24, 'PoiRevise', 'POI修正', NULL, 7, '2024-08-07 11:16:12', 1, 0, NULL, 0);\r\n";
            string t_scgd_sys_dictionary_mod_item = "INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (5200, 'M', 5200, 'M', 1, NULL, '10', 24, '2024-08-07 11:16:28', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (5201, 'N', 5201, 'N', 1, NULL, '10', 24, '2024-08-07 11:16:30', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (5202, 'P', 5202, 'P', 1, NULL, '10', 24, '2024-08-07 11:16:33', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (5203, 'GenCalibrationType', 5203, 'GenCalibrationType', 1, NULL, 'BrightnessAndChroma', 24, '2024-08-08 11:37:11', 1, 0, NULL);\r\n";

            return t_scgd_sys_dictionary_mod_master + t_scgd_sys_dictionary_mod_item;
        }
    }
}
