using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.OLEDImageProcessing
{
    public class MysqlLocalizationImageEnhancement : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复局部图像增强";

        public string GetRecover()
        {
            return """
INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (201, 'OLED.LocalizationImageEnhan', '局部图像增强', 1, NULL, 7, '{"bs": 16, "sp": 100, "mapping": 5.5, "tgsigma": 0.9166666666666666, "blurSize": 31, "th_ratio": 0.1, "threshold": 5, "img_format_convert_factor": 256}', NULL, '2026-07-09 10:01:38', 1, 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`), `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);
""";
        }
    }
}
