using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.Jsons.DataConvert
{
    public class MysqlDataConvert : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复数据转换模板";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (160, 'Math.DataConvert', '数据转换', 1, NULL, 160, '{\\\"Fit\\\": 5, \\\"FileName\\\": \\\"\\\"}', '2024-11-22 10:36:21', 1, 0, NULL, 0);\r\n";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
