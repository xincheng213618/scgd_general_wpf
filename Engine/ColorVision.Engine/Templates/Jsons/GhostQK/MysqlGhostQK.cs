using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.Jsons.GhostQK
{
    public class MysqlGhostQK : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复GhostQK";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (38, 'ghost_qk', '鬼影', 1, NULL, 7, '{}', '2024-04-28 11:38:32', 1, 0, NULL, 0);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
