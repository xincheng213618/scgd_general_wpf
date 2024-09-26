using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.ROI
{
    public class MysqlRoi : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复MysqlMysqlROI";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "UPDATE `t_scgd_sys_dictionary_mod_master` SET `code` = 'OLED.GetROI', `name` = 'ROI', `pid` = NULL, `mod_type` = 7, `create_date` = '2024-08-16 15:27:12', `is_enable` = 1, `is_delete` = 0, `remark` = NULL, `tenant_id` = 0 WHERE `id` = 31;\r\n";
            string t_scgd_sys_dictionary_mod_item = "INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4310, 'Threshold', 4310, '阈值', 0, NULL, '1', 31, '2023-11-14 17:44:15', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4311, 'Times', 4311, 'Times', 0, NULL, '1', 31, '2023-11-14 17:44:15', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4312, 'SmoothSize', 4312, 'SmoothSize', 0, NULL, '1', 31, '2023-11-14 17:44:15', 1, 0, NULL);";
            return t_scgd_sys_dictionary_mod_master + t_scgd_sys_dictionary_mod_item;
        }
    }
}
