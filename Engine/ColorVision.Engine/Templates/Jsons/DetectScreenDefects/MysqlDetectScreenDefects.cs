using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.DetectScreenDefects
{
    public class MysqlDetectScreenDefects : IMysqlCommand
    {
        public string GetMysqlCommandName() => Properties.Resources.ScreenDefect_RestoreDatabase;

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = """
INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (58, 'ARVR.DetectScreenDefects', '屏幕缺陷检测', 1, NULL, 7, '{"screenDefectCfg": {"blur_sigma": 1.35, "line_params": {"angle": 0, "length": 15, "threshold": 0.95}, "defect_sizes": [25, 15, 5], "aa_min_ratio_h": 0.2, "aa_min_ratio_w": 0.2, "merge_distance": 10, "clarity_threshold": 0.6, "defect_thresholds": [0.25, 0.25, 0.5], "clarity_safe_margin": 55, "light_shrink_margin": 60, "brightness_threshold": 0.3}}', NULL, '2026-07-09 10:01:38', 1, 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`), `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);
""";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
