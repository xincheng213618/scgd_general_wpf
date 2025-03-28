using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.Jsons.SFRFindROI
{
    public class MysqlSFRFindROI : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复MysqlSFRFindROI";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (36, 'ARVR.SFR.FindROI', 'SFR寻边', 1, NULL, 7, '{\\\"th\\\": 0.6, \\\"roi_h\\\": 60, \\\"roi_w\\\": 60, \\\"minLength\\\": 100, \\\"lowThreshold\\\": 20, \\\"highThreshold\\\": 40}', '2024-11-05 11:25:17', 1, 0, NULL, 0);\r\n";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
