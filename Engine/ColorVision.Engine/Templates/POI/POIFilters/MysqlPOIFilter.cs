using ColorVision.Database;

namespace ColorVision.Engine.Templates.POI.POIFilters
{
    public class MysqlPOIFilter : IMysqlCommand
    {
        public string GetMysqlCommandName() => "POI过滤模板";
        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `pid`, `mod_type`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (23, 'POIFilter', 'POIFliter', NULL, 112, '2024-07-23 14:25:21', 1, 0, NULL, 0);\r\n";
            string t_scgd_sys_dictionary_mod_item = "INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (5100, 'NoAreaEnable', 5100, 'NoAreaEnable', 2, NULL, 'false', 23, '2024-07-23 14:17:23', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (5101, 'Enable', 5101, 'Enable', 2, NULL, 'false', 23, '2024-07-23 14:17:27', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (5102, 'XYZEnable', 5102, 'XYZEnable', 2, NULL, 'false', 23, '2024-07-23 14:17:28', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (5103, 'XYZType', 5103, 'XYZType', 0, NULL, '1', 23, '2024-10-12 11:21:46', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (5104, 'Threshold', 5104, 'Threshold', 1, NULL, '50', 23, '2024-10-12 11:30:42', 1, 0, NULL);\r\nINSERT INTO`t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (5105, 'ThresholdUsePercent', 5105, 'ThresholdUsePercent', 2, NULL, 'false', 23, '2024-07-26 10:11:48', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (5106, 'MaxPercent', 5106, 'MaxPercent', 1, NULL, '1', 23, '2024-08-07 11:35:40', 1, 0, NULL);";
            return t_scgd_sys_dictionary_mod_master + t_scgd_sys_dictionary_mod_item;
        }
    }
}
