using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.CameraExposure
{
    public class MysqCameraExposure : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复Mysql CameraExposure模板设置";
        public string GetRecover()
        {

            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (200, 'AutoFocus', '自动聚焦', 0, NULL, 1, NULL, '2025-01-22 11:19:06', 1, 0, NULL, 0);\r\n";
            string t_scgd_sys_dictionary_mod_item = "INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (21, 'ExpTime', 21, '曝光时间', 0 , NULL, '80', 20, '2024-04-26 11:54:10', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (22, 'ExpTimeR', 22, '曝光时间R', 0 , NULL, '80', 20, '2024-04-26 11:57:07', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (23, 'ExpTimeG', 23, '曝光时间G', 0 , NULL, '80', 20, '2024-04-26 11:57:09', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (24, 'ExpTimeB', 24, '曝光时间B', 0 , NULL, '80', 20, '2024-04-26 11:57:11', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (25, 'EnableFocus', 25, 'EnableFocus', 2 , NULL, 'false', 20, '2024-04-26 11:57:07', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (26, 'AvgCount', 26, 'AvgCount', 0 , NULL, 1, 20, '2024-04-26 11:57:09', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (27, 'Focus', 27, 'Focus', 0 , NULL, -1, 20, '2024-04-26 11:57:11', 1 , 0, NULL);  INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type` , `value_range`, `default_val`, `pid`, `create_date`, `is_enable` , `is_delete`, `remark`) VALUES (28, 'Aperture', 28, 'Aperture', 0 , NULL, -1, 20, '2024-04-26 11:57:11', 1 , 0, NULL);INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (20, 'Gain', 20, 'Gain', 0, NULL, '0', 20, '2025-02-20 10:01:16', 1, 0, NULL);";
            return t_scgd_sys_dictionary_mod_master + t_scgd_sys_dictionary_mod_item;
        }

    }
}
