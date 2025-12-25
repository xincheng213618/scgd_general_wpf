using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI
{
    public class MysqlOLEDAOI : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复OLED_AOI";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (28, 'OLED.AOI', 'OLED AOI', 1, NULL, 7, '{\"rebuiltImgPixelDefects\": {\"dark_lvs\": [1, 2, 3, 4, 6], \"bright_lvs\": [1, 2, 3, 4, 6], \"edge_area_h\": 380, \"edge_area_w\": 520, \"edge_area_x\": 80, \"edge_area_y\": 50, \"gradingCfgs\": [{\"grade_name\": \"GOOD\", \"dark_max_nums\": [2, 1, 0, 0, 0], \"bright_max_nums\": [2, 1, 0, 0, 0]}, {\"grade_name\": \"WELL\", \"dark_max_nums\": [2000, 100, 50, 10, 1], \"bright_max_nums\": [2000, 100, 50, 10, 1]}, {\"grade_name\": \"SOSO\", \"dark_max_nums\": [3000, 100, 50, 10, 1], \"bright_max_nums\": [3000, 100, 50, 10, 1]}], \"enable_grading\": true, \"darkThreshold_edge_area\": 0.15, \"brightThreshold_edge_area\": 1, \"darkPixelLvRatioThreshold\": 0.4, \"brightPixelLvRatioThreshold\": 1, \"averageMaskLvRatioThresholdDark\": 0.4, \"averageMaskLvRatioThresholdBright\": 2}}', NULL, '2025-12-11 11:39:22', 1, 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`) , `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`) , `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
