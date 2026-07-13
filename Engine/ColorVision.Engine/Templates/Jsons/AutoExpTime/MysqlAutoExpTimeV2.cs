using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.AutoExpTime
{
    public class MysqlAutoExpTimeV2 : IMysqlCommand
    {
        public string GetMysqlCommandName() => "Recover AutoExpTime V2";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (94, 'auto_exp_time', '\u81ea\u52a8\u66dd\u5149', 1, NULL, 1, '{\"expTimeCfg\": {\"type\": 0, \"maxExpTime\": 60000, \"minExpTime\": 10, \"autoExpFlag\": false, \"autoExpSatDev\": 20, \"RoiMarginRatio\": {\"margin_RatioTop\": 0, \"margin_RatioLeft\": 0, \"margin_RatioRight\": 0, \"margin_RatioBottom\": 0}, \"burstThreshold\": 200, \"autoExpSatMaxAD\": 65535, \"autoExpSyncFreq\": 60, \"autoExpTimeBegin\": 5, \"autoExpSaturation\": 70, \"autoExpMaxPecentage\": 0.01}}', '2.0', '2024-04-28 11:38:01', 1, 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`), `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
