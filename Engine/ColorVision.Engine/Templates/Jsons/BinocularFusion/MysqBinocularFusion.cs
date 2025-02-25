using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.Jsons.BinocularFusion
{
    public class MysqBinocularFusion : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复MysqBinocularFusion";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (35, 'ARVR.BinocularFusion', '双目融合', 1, NULL, 7, '{\\\"center_pt\\\": {\\\"x\\\": 0, \\\"y\\\": 0}, \\\"crossMarks\\\": 5, \\\"debugOutPath\\\": \\\"\\\", \\\"focus_length\\\": 30, \\\"thh_ratio_hor\\\": 0.1, \\\"knl_size_erode\\\": 125, \\\"thh_ratio_vert\\\": 0.15, \\\"knl_size_smooth\\\": 5, \\\"size_coms_pixel\\\": 3.76, \\\"threshold_binary\\\": 21, \\\"distance_XR_target_image\\\": 3000}', '2024-11-05 11:25:17', 1, 0, NULL, 0);\r\n";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
