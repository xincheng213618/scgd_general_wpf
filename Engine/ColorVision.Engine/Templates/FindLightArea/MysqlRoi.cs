using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.FindLightArea
{
    public class MysqlRoi : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复Mysql发光区检测";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid` , `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable` , `is_delete`, `remark`, `tenant_id`) VALUES (15, 'FindLightArea', '发光区检测', 0, NULL , 7, NULL, '0.0', '2024-04-28 11:40:20', 1 , 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = 'FindLightArea', `name` = '发光区检测', `p_type` = 0, `pid` = NULL, `mod_type` = 7 , `cfg_json` = NULL, `version` = '0.0', `create_date` = '2024-04-28 11:40:20', `is_enable` = 1, `is_delete` = 0 , `remark` = NULL, `tenant_id` = 0;";
            string t_scgd_sys_dictionary_mod_item = "INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4310, 'Threshold', 4310, '阈值', 0, NULL, '1', 31, '2023-11-14 17:44:15', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4311, 'Times', 4311, 'Times', 0, NULL, '1', 31, '2023-11-14 17:44:15', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4312, 'SmoothSize', 4312, 'SmoothSize', 0, NULL, '1', 31, '2023-11-14 17:44:15', 1, 0, NULL);";
            return t_scgd_sys_dictionary_mod_master + t_scgd_sys_dictionary_mod_item;
        }
    }
}
