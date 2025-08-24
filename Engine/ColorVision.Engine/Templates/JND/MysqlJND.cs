using ColorVision.Database;

namespace ColorVision.Engine.Templates.JND
{
    public class MysqlJND : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复MysqlJND";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `pid`, `mod_type`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (30, 'OLED.JND.CalVas', 'JND', NULL, 7, '2024-08-16 15:27:12', 1, 0, NULL, 0);\r\n";
            string t_scgd_sys_dictionary_mod_item = "INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4300, 'CutOff', 4300, '轮廓裁剪系数', 1, NULL, '0.3', 30, '2023-11-14 17:44:15', 1, 0, NULL);\r\n";
            return t_scgd_sys_dictionary_mod_master + t_scgd_sys_dictionary_mod_item;
        }
    }
}
