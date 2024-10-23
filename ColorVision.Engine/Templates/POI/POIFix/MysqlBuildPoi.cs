using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Services.Templates.POI.POIFix
{
    public class MysqlPoiFix:IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复MysqlPoiFix";
        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `pid`, `mod_type`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (109, 'PoiFix', 'PoiFix', NULL, 112, '2024-10-23 11:17:26', 1, 0, NULL, 0);\r\n";
            string t_scgd_sys_dictionary_mod_item = "INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (6001, 'PoiFixFilePath', 6001, 'PoiFixFilePath', 3, NULL, NULL, 109, '2024-10-23 11:19:57', 1, 0, NULL);";
            return t_scgd_sys_dictionary_mod_master + t_scgd_sys_dictionary_mod_item;
        }
    }
}
