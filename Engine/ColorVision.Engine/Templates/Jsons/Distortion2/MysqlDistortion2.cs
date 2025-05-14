using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.Jsons.Distortion2
{
    public class MysqlDistortion2 : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复Distortion2.0";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid` , `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable` , `is_delete`, `remark`, `tenant_id`) VALUES (40, 'distortion', '畸变V2', 1, NULL , 7, '{\"MaskRect\": {\"h\": 0, \"w\": 0, \"x\": 0, \"y\": 0, \"enable\": false}, \"debugCfg\": {\"Debug\": false, \"debugPath\": \"Result\\\\\\\\\", \"debugImgResize\": 2}, \"CommonParams\": {\"pattern\": 2, \"brightNumX\": 3, \"brightNumY\": 3}, \"Point9Params\": {\"erodeTime\": 0, \"threshold\": 20000, \"erodeKernel\": 3, \"outRectSizeMax\": 400, \"outRectSizeMin\": 40}, \"caclDistorType\": {\"DistortionTV\": true, \"DistortionOptic\": true, \"Distortion9Point\": true}, \"ClassicalParams\": {\"timeOut\": 50000, \"slopeType\": 0, \"layoutType\": 0, \"blobThreParams\": {\"maxArea\": 10000, \"minArea\": 200, \"bgRadius\": 31, \"blobColor\": 0, \"darkRatio\": 0.01, \"filterByArea\": true, \"maxConvexity\": 3.4028235e38, \"maxThreshold\": 220, \"minConvexity\": 0.9, \"minThreshold\": 10, \"contrastRatio\": 0.1, \"filterByColor\": true, \"thresholdStep\": 10, \"maxCircularity\": 3.4028235e38, \"minCircularity\": 0.9, \"filterByInertia\": false, \"maxInertiaRatio\": 3.4028235e38, \"minInertiaRatio\": 0.1, \"minRepeatability\": 2, \"filterByConvexity\": false, \"filterByCircularity\": false, \"minDistBetweenBlobs\": 50}}}', '2.0', '2024-04-28 11:38:32', 1 , 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`) , `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`) , `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
