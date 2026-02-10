using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForBlackScreen
{
    public class MysqlFPForBlackScreen : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复黑画面检测";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (57, 'OLED.AOI.FPForBlackScreen', '黑画面检测', 1, NULL, 7, '{\"TimeStamp\": \"_20251231_145129\", \"GradeLevel\": \"NG\"}', NULL, '2025-12-11 11:39:22', 1, 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`) , `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`) , `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
