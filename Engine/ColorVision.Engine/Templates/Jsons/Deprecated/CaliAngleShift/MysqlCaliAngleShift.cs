using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.CaliAngleShift
{
    public class MysqlCaliAngleShift : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复色差校正";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (51, 'CaliAngleShift', '色差校正', 1, NULL, 7, '{\"coeff_b\": [-2.05705963006213, -0.203402567360732, -0.00000871816081889568, 0.00000000985087345114056, -0.00000000000104340220586239, 1.38791315222617e-16, -8.29378026031862e-21], \"coeff_g\": [-2.1051636728357, -0.204828890611705, -0.00000786286597975059, 0.00000000969936743625218, -0.00000000000110631282749306, 1.56962468799272e-16, -9.48550126449628e-21], \"coeff_r\": [-2.10810283209893, -0.208726349137694, -0.00000959350167719098, 0.000000009837748576948453, -0.000000000000888646394378553, 9.502897498916671e-17, -4.70095868790552e-21], \"caliType\": 15, \"vamAngle\": 60, \"target_col\": 6280, \"target_row\": 4210, \"rowColShift\": [0, 0], \"optical_center_x\": 3140, \"optical_center_y\": 2105, \"coefficient_order\": 6, \"interpolate_ratio\": 3}', '1.0', '2025-12-30 15:39:39', 1, 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`), `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
