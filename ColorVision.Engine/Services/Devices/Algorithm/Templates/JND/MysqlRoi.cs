using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.JND
{
    public class MysqlJND : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复MysqlJND";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "UPDATE `t_scgd_sys_dictionary_mod_master` SET `code` = 'OLED.JND.CalVas', `name` = 'JND', `pid` = NULL, `mod_type` = 7, `create_date` = '2024-08-16 15:27:12', `is_enable` = 1, `is_delete` = 0, `remark` = NULL, `tenant_id` = 0 WHERE `id` = 30;\r\n";
            string t_scgd_sys_dictionary_mod_item = "UPDATE `t_scgd_sys_dictionary_mod_item` SET `symbol` = 'CutOff', `address_code` = 4300, `name` = '轮廓裁剪系数', `val_type` = 1, `value_range` = NULL, `default_val` = '0.3', `pid` = 30, `create_date` = '2023-11-14 17:44:15', `is_enable` = 1, `is_delete` = 0, `remark` = NULL WHERE `id` = 4300;\r\n";
            return t_scgd_sys_dictionary_mod_master + t_scgd_sys_dictionary_mod_item;
        }
    }
}
