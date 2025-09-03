using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.Ghost2
{
    public class MysqlGhost2 : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复Ghost2.0";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid` , `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable` , `is_delete`, `remark`, `tenant_id`) VALUES (38, 'ghost', '鬼影V2', 1, NULL , 7, '{\"Ring\": {\"range\": 5, \"erodeTime\": 1, \"erodeKernel\": 3, \"peakDistance\": [60, 40], \"outRectSizeMin\": 400, \"thresholdValue\": 20000, \"outRectSizeRate\": 10}, \"Ghost\": {\"minGary\": -1, \"garyRate\": 1, \"erodeTime\": 5, \"dilateTime\": 0, \"erodeKernel\": 3, \"dilateKernel\": 3, \"thresholdMax\": 500, \"thresholdMin\": 50, \"thresholdStep\": 10, \"outRectSizeMin\": 100, \"outRectSizeRate\": 7.3, \"distanceToBright\": 100, \"ingoreCheckMixBright\": [false, false, false, false, true, false, false, false, false]}, \"Bright\": {\"brightNumX\": 3, \"brightNumY\": 3, \"erodeKernel\": 3, \"patternType\": 0, \"thresholdMax\": 40000, \"thresholdMin\": 30000, \"thresholdStep\": 1000, \"outRectSizeMin\": 60, \"outRectSizeRate\": 5}, \"vOther\": {\"Debug\": false, \"debugPath\": \"Result\\\\\\\\\", \"showMaxGain\": 0.8, \"showMinGain\": 60, \"debugImgResize\": 2}, \"MaskRect\": {\"h\": 0, \"w\": 0, \"x\": 0, \"y\": 0, \"enable\": false}}', '2.0', '2024-04-28 11:38:32', 1 , 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`) , `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`) , `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
