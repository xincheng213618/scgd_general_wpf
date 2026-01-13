using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.ImageROI
{
    public class MysqlImageROI : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复图像裁剪模板";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (52, 'Image.ROI', '图像裁剪', 1, NULL, 7, '{\"RHO\": 60, \"center\": {\"x\": 3126, \"y\": 2088}, \"pixelToAngle\": 0.02645}', '1.0', '2025-12-30 15:39:39', 1, 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = 'Image.ROI', `name` = '图像裁剪', `p_type` = 1, `pid` = NULL, `mod_type` = 7, `cfg_json` = '{\"RHO\": 60, \"center\": {\"x\": 3126, \"y\": 2088}, \"pixelToAngle\": 0.02645}', `version` = '1.0', `create_date` = '2025-12-30 15:39:39', `is_enable` = 1, `is_delete` = 0, `remark` = NULL, `tenant_id` = 0;";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
