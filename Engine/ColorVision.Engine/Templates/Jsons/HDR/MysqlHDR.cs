using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.Jsons.HDR
{
    public class MysqlHDR : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复HDR模板";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid` , `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable` , `is_delete`, `remark`, `tenant_id`) VALUES (43, 'camera_exp_time', '相机参数', 1, NULL , 1, '{\"ThLow\": 50, \"ThHigh\": 150, \"ExpTimes\": [10, 50, 100], \"HDRExpTime\": 100}', 'HDR', '2024-04-28 11:35:49', 1 , 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = 'camera_exp_time', `name` = '相机参数', `p_type` = 1, `pid` = NULL, `mod_type` = 1 , `cfg_json` = '{\"ThLow\": 50, \"ThHigh\": 150, \"ExpTimes\": [10, 50, 100], \"HDRExpTime\": 100}', `version` = 'HDR', `create_date` = '2024-04-28 11:35:49', `is_enable` = 1, `is_delete` = 0 , `remark` = NULL, `tenant_id` = 0;";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
