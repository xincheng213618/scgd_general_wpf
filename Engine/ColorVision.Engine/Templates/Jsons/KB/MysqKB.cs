using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.Jsons.KB
{
    public class MysqKB : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复MysqKB";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (150, 'KB', 'KB', 1, NULL, 150, '{\\\"keyRect\\\": [{\\\"h\\\": 171, \\\"w\\\": 272, \\\"x\\\": 208, \\\"y\\\": 1073, \\\"key\\\": {\\\"move\\\": 20, \\\"offset_X\\\": 0, \\\"offset_Y\\\": 0, \\\"thresholdV\\\": 5000}, \\\"halo\\\": {\\\"move\\\": 20, \\\"haloSize\\\": 15, \\\"offset_X\\\": 0, \\\"offset_Y\\\": 0, \\\"thresholdV\\\": 300}, \\\"name\\\": \\\"q\\\", \\\"doKey\\\": true, \\\"doHalo\\\": true}, {\\\"h\\\": 255, \\\"w\\\": 273, \\\"x\\\": 2127, \\\"y\\\": 1245, \\\"key\\\": {\\\"move\\\": 25, \\\"haloSize\\\": 15, \\\"offset_X\\\": 0, \\\"offset_Y\\\": 0, \\\"thresholdV\\\": 3000}, \\\"halo\\\": {\\\"move\\\": 20, \\\"haloSize\\\": 15, \\\"offset_X\\\": 0, \\\"offset_Y\\\": 0, \\\"thresholdV\\\": 300}, \\\"name\\\": \\\"7\\\", \\\"doKey\\\": true, \\\"doHalo\\\": true}], \\\"debugPath\\\": \\\"D:\\\\\\\\Tiff\\\\\\\\\\\", \\\"saveProcessData\\\": false}', '2024-11-22 10:36:21', 1, 0, NULL, 0);\r\n";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
