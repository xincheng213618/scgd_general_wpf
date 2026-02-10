using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForRePicGradingV2
{
    public class MysqlFPForRePicGradingV2 : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复缺陷检测V2";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (56, 'OLED.AOI.FPForRePicGradingV2', '缺陷检测V2', 1, NULL, 7, '{\"width\": 12, \"height\": 12, \"radius\": 6, \"dark_lvs\": [1, 2, 3, 4, 6], \"lowRatio\": 10, \"topRatio\": 10, \"scan_type\": 1, \"bright_lvs\": [1, 2, 3, 4, 6], \"edge_area_h\": 120, \"edge_area_w\": 160, \"edge_area_x\": 239, \"edge_area_y\": 179, \"faultPixelRatio\": 4, \"badPixelNumThreshold\": 102, \"brightPixelNumThreshold\": 70, \"darkPixelLvRatioThreshold\": 0.4, \"brightPixelLvRatioThreshold\": 1.2, \"connectedBadPixelNumThreshold\": 10}', NULL, '2025-12-11 11:39:22', 1, 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`) , `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`) , `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
