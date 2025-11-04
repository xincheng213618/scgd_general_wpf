using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.SFR2
{
    public class MysqlSFR2 : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复SFR_2.0";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (49, 'SFR', 'SFR_V2', 1, NULL, 7, '{\"caclWay\": 1, \"MaskRect\": {\"h\": 0, \"w\": 0, \"x\": 0, \"y\": 0, \"enable\": false}, \"debugCfg\": {\"Debug\": false, \"debugPath\": \"Result\\\\\\\\\", \"debugImgResize\": 2}, \"sfrAutoPoi1\": {\"dst_roi_h\": 60, \"dst_roi_w\": 60, \"minLength\": 100, \"active_Top\": true, \"active_Left\": true, \"active_Right\": true, \"lowThreshold\": 20, \"active_Bottom\": true, \"highThreshold\": 40, \"thresholdRatio\": 0.6}}', '2.0', '2024-04-28 11:38:34', 1, 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`), `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
