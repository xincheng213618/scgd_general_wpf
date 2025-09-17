using ColorVision.Database;

namespace ColorVision.Engine.Templates.POI.POIOutput
{
    public class MysqlPoiOutput : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复Mysql Poi文件输出模板设置";
        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master =
                "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `pid`, `mod_type`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) " +
                "VALUES (27, 'PoiOutput', 'POI输出', NULL, 7, '2024-08-16 15:27:12', 1, 0, NULL, 0) " +
                "ON DUPLICATE KEY UPDATE " +
                "`code`=VALUES(`code`), `name`=VALUES(`name`), `pid`=VALUES(`pid`), `mod_type`=VALUES(`mod_type`), `create_date`=VALUES(`create_date`), `is_enable`=VALUES(`is_enable`), `is_delete`=VALUES(`is_delete`), `remark`=VALUES(`remark`), `tenant_id`=VALUES(`tenant_id`);\r\n";

            string t_scgd_sys_dictionary_mod_item =
                "INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES " +
                "(5520, 'MaskFileName', 5520, 'MaskFileName', 3, NULL, '', 27, '2025-09-17 13:21:04', 1, 0, NULL)," +
                "(5500, 'XIsEnable', 5500, 'XIsEnable', 2, NULL, 'false', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5501, 'XFileName', 5501, 'XFileName', 3, NULL, 'X.tif', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5502, 'YIsEnable', 5502, 'YIsEnable', 2, NULL, 'false', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5503, 'YFileName', 5503, 'YFileName', 3, NULL, 'Y.tif', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5504, 'ZIsEnable', 5504, 'ZIsEnable', 2, NULL, 'false', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5505, 'ZFileName', 5505, 'ZFileName', 3, NULL, 'Z.tif', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5506, 'xIsEnable', 5506, 'xIsEnable', 2, NULL, 'false', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5507, 'xFileName', 5507, 'xFileName', 3, NULL, 'x1.tif', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5508, 'yIsEnable', 5508, 'yIsEnable', 2, NULL, 'false', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5509, 'yFileName', 5509, 'yFileName', 3, NULL, 'y1.tif', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5510, 'uIsEnable', 5510, 'uIsEnable', 2, NULL, 'false', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5511, 'uFileName', 5511, 'uFileName', 3, NULL, 'u1.tif', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5512, 'vIsEnable', 5512, 'vIsEnable', 2, NULL, 'false', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5513, 'vFileName', 5513, 'vFileName', 3, NULL, 'v1.tif', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5514, 'CCTIsEnable', 5514, 'CCTIsEnable', 2, NULL, 'false', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5515, 'CCTFileName', 5515, 'CCTFileName', 3, NULL, 'CCT.tif', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5516, 'WaveIsEnable', 5516, 'WaveIsEnable', 2, NULL, 'false', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5517, 'WaveFileName', 5517, 'WaveFileName', 3, NULL, 'Wave.tif', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5518, 'Height', 5518, 'Height', 0, NULL, '0', 27, '2024-08-06 12:46:31', 1, 0, NULL)," +
                "(5519, 'Width', 5519, 'Width', 0, NULL, '0', 27, '2024-08-06 12:46:31', 1, 0, NULL) " +
                "ON DUPLICATE KEY UPDATE " +
                "`symbol`=VALUES(`symbol`), `address_code`=VALUES(`address_code`), `name`=VALUES(`name`), `val_type`=VALUES(`val_type`), `value_range`=VALUES(`value_range`), `default_val`=VALUES(`default_val`), `pid`=VALUES(`pid`), `create_date`=VALUES(`create_date`), `is_enable`=VALUES(`is_enable`), `is_delete`=VALUES(`is_delete`), `remark`=VALUES(`remark`);\r\n";

            return t_scgd_sys_dictionary_mod_master + t_scgd_sys_dictionary_mod_item;
        }
    }
}
