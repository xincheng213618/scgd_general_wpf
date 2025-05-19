using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.Jsons.LargeFlow
{
    public class MysqlLargeFlow : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复MysqlLargeFlow";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO  `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid` , `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable` , `is_delete`, `remark`, `tenant_id`) VALUES (999, 'LargeFlow', '大流程', 0, NULL , 1, NULL, NULL, '2025-05-15 14:19:56', 1 , 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = 'LargeFlow', `name` = '大流程', `p_type` = 0, `pid` = NULL, `mod_type` = 1 , `cfg_json` = NULL, `version` = NULL, `create_date` = '2025-05-15 14:19:56', `is_enable` = 1, `is_delete` = 0 , `remark` = NULL, `tenant_id` = 0;";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
