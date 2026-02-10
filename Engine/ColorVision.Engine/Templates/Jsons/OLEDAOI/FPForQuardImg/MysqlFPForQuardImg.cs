using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForQuardImg
{
    public class MysqlFPForQuardImg : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复亮点检测";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (55, 'OLED.AOI.FPForQuardImg', '亮点检测', 1, NULL, 7, '{\"th\": 0.1, \"index\": 1, \"num_th\": 10}', NULL, '2025-12-11 11:39:22', 1, 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`) , `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`) , `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
