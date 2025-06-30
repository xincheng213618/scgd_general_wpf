using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.Jsons.FindCross
{
    public class MysqlFindCross : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复MysqlFindCross";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master`\r\n(`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`)\r\nVALUES\r\n(45, 'FindCross', '十字计算', 1, NULL, 7, '{\\\"debugCfg\\\": {\\\"Debug\\\": false, \\\"debugPath\\\": \\\"Result\\\\\\\\\\\", \\\"debugImgResize\\\": 2}, \\\"CheckLine\\\": {\\\"rho\\\": 2, \\\"houghV\\\": 100, \\\"floAngle\\\": 10, \\\"erodeTime\\\": 1, \\\"maxLineGap\\\": 10, \\\"erodeKernel\\\": 3, \\\"minLineLength\\\": 40}, \\\"mathMaskRect\\\": {\\\"h\\\": 0, \\\"w\\\": 0, \\\"x\\\": 0, \\\"y\\\": 0, \\\"enable\\\": false}}', '1.0', '2025-05-26 14:34:48', 1, 0, NULL, 0)\r\nON DUPLICATE KEY UPDATE\r\n`code` = VALUES(`code`),\r\n`name` = VALUES(`name`),\r\n`p_type` = VALUES(`p_type`),\r\n`pid` = VALUES(`pid`),\r\n`mod_type` = VALUES(`mod_type`),\r\n`cfg_json` = VALUES(`cfg_json`),\r\n`version` = VALUES(`version`),\r\n`create_date` = VALUES(`create_date`),\r\n`is_enable` = VALUES(`is_enable`),\r\n`is_delete` = VALUES(`is_delete`),\r\n`remark` = VALUES(`remark`),\r\n`tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
