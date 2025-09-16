using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.LEDStripDetectionV2
{
    public class MysqlLEDStripDetectionV2 : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复灯条检测V2";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` \r\n(`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`)\r\nVALUES \r\n(26, 'LEDStripDetection', '灯条检测V2', 1, NULL, 7, '{\\\"debugCfg\\\": {\\\"Debug\\\": false, \\\"debugPath\\\": \\\"Result\\\\\\\\\\\", \\\"debugImgResize\\\": 2}, \\\"caliRatio\\\": 10, \\\"threshold\\\": 25, \\\"erodeAndDiate\\\": {\\\"erodeTime\\\": 0, \\\"dilateTime\\\": 0, \\\"erodeFirst\\\": true, \\\"erodeKernel\\\": 3, \\\"dilateKernel\\\": 3}, \\\"binaryByContours\\\": true, \\\"binaryRateInContours\\\": 0.35}', '2.0', '2024-06-06 16:12:41', 1, 0, NULL, 0)\r\nON DUPLICATE KEY UPDATE\r\n  `code` = VALUES(`code`),\r\n  `name` = VALUES(`name`),\r\n  `p_type` = VALUES(`p_type`),\r\n  `pid` = VALUES(`pid`),\r\n  `mod_type` = VALUES(`mod_type`),\r\n  `cfg_json` = VALUES(`cfg_json`),\r\n  `version` = VALUES(`version`),\r\n  `create_date` = VALUES(`create_date`),\r\n  `is_enable` = VALUES(`is_enable`),\r\n  `is_delete` = VALUES(`is_delete`),\r\n  `remark` = VALUES(`remark`),\r\n  `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
