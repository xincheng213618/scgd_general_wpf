using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.ROI
{
    public class MysqlRoi : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复Mysql发光区检测";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `pid`, `mod_type`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (31, 'OLED.GetROI', 'ROI', NULL, 7, '2024-08-16 15:27:12', 1, 0, NULL, 0);\r\n";
            string t_scgd_sys_dictionary_mod_item = "INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4310, 'Threshold', 4310, '阈值', 0, NULL, '1', 31, '2023-11-14 17:44:15', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4311, 'Times', 4311, 'Times', 0, NULL, '1', 31, '2023-11-14 17:44:15', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4312, 'SmoothSize', 4312, 'SmoothSize', 0, NULL, '1', 31, '2023-11-14 17:44:15', 1, 0, NULL);";
            return t_scgd_sys_dictionary_mod_master + t_scgd_sys_dictionary_mod_item;
        }
    }
}
