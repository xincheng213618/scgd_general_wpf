using ColorVision.Database;
using System.Collections.ObjectModel;
using System.Text;

namespace ColorVision.Engine.Templates.ARVR.AOI
{
    public class TemplateAOIParam : ITemplate<AOIParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<AOIParam>> Params { get; set; } = new ObservableCollection<TemplateModel<AOIParam>>();

        public TemplateAOIParam()
        {
            Title = "AOIParamConfig";
            Code = "AOI";
            TemplateDicId = 12;
            TemplateParams = Params;
        }
        public override IMysqlCommand? GetMysqlCommand() => new MysqlAOI();
    }

    public class MysqlAOI : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复MysqlAOI";

        public string GetRecover()
        {
            StringBuilder sb = new StringBuilder();

            // 1201 Left
            sb.Append("INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (1201, 'Left', 1201, '左', 0, NULL, '5', 12, '2023-07-06 17:21:18', 1, 0, NULL) ");
            sb.Append("ON DUPLICATE KEY UPDATE `symbol` = VALUES(`symbol`), `address_code` = VALUES(`address_code`), `name` = VALUES(`name`), `val_type` = VALUES(`val_type`), `value_range` = VALUES(`value_range`), `default_val` = VALUES(`default_val`), `pid` = VALUES(`pid`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`); ");

            // 1202 Right
            sb.Append("INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (1202, 'Right', 1202, '右', 0, NULL, '5', 12, '2023-07-06 17:21:19', 1, 0, NULL) ");
            sb.Append("ON DUPLICATE KEY UPDATE `symbol` = VALUES(`symbol`), `address_code` = VALUES(`address_code`), `name` = VALUES(`name`), `val_type` = VALUES(`val_type`), `value_range` = VALUES(`value_range`), `default_val` = VALUES(`default_val`), `pid` = VALUES(`pid`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`); ");

            // 1203 Top
            sb.Append("INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (1203, 'Top', 1203, '上', 0, NULL, '5', 12, '2023-07-06 17:21:20', 1, 0, NULL) ");
            sb.Append("ON DUPLICATE KEY UPDATE `symbol` = VALUES(`symbol`), `address_code` = VALUES(`address_code`), `name` = VALUES(`name`), `val_type` = VALUES(`val_type`), `value_range` = VALUES(`value_range`), `default_val` = VALUES(`default_val`), `pid` = VALUES(`pid`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`); ");

            // 1204 Bottom
            sb.Append("INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (1204, 'Bottom', 1204, '下', 0, NULL, '5', 12, '2023-07-06 17:21:21', 1, 0, NULL) ");
            sb.Append("ON DUPLICATE KEY UPDATE `symbol` = VALUES(`symbol`), `address_code` = VALUES(`address_code`), `name` = VALUES(`name`), `val_type` = VALUES(`val_type`), `value_range` = VALUES(`value_range`), `default_val` = VALUES(`default_val`), `pid` = VALUES(`pid`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`); ");

            // 1205 BlurSize
            sb.Append("INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (1205, 'BlurSize', 1205, 'BlurSize', 0, NULL, '19', 12, '2023-07-06 17:53:30', 1, 0, NULL) ");
            sb.Append("ON DUPLICATE KEY UPDATE `symbol` = VALUES(`symbol`), `address_code` = VALUES(`address_code`), `name` = VALUES(`name`), `val_type` = VALUES(`val_type`), `value_range` = VALUES(`value_range`), `default_val` = VALUES(`default_val`), `pid` = VALUES(`pid`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`); ");

            // 1206 DilateSize
            sb.Append("INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (1206, 'DilateSize', 1206, 'DilateSize', 0, NULL, '5', 12, '2023-07-06 18:09:19', 1, 0, NULL) ");
            sb.Append("ON DUPLICATE KEY UPDATE `symbol` = VALUES(`symbol`), `address_code` = VALUES(`address_code`), `name` = VALUES(`name`), `val_type` = VALUES(`val_type`), `value_range` = VALUES(`value_range`), `default_val` = VALUES(`default_val`), `pid` = VALUES(`pid`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`); ");

            // 1207 FilterByContrast
            sb.Append("INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (1207, 'FilterByContrast', 1207, 'FilterByContrast', 2, NULL, 'True', 12, '2023-07-06 18:16:56', 1, 0, NULL) ");
            sb.Append("ON DUPLICATE KEY UPDATE `symbol` = VALUES(`symbol`), `address_code` = VALUES(`address_code`), `name` = VALUES(`name`), `val_type` = VALUES(`val_type`), `value_range` = VALUES(`value_range`), `default_val` = VALUES(`default_val`), `pid` = VALUES(`pid`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`); ");

            // 1208 MaxContrast
            sb.Append("INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (1208, 'MaxContrast', 1208, 'MaxContrast', 1, NULL, '1.7', 12, '2023-07-06 18:20:12', 1, 0, NULL) ");
            sb.Append("ON DUPLICATE KEY UPDATE `symbol` = VALUES(`symbol`), `address_code` = VALUES(`address_code`), `name` = VALUES(`name`), `val_type` = VALUES(`val_type`), `value_range` = VALUES(`value_range`), `default_val` = VALUES(`default_val`), `pid` = VALUES(`pid`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`); ");

            // 1209 MinContrast
            sb.Append("INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (1209, 'MinContrast', 1209, 'MinContrast', 1, NULL, '0.3', 12, '2023-07-06 18:20:32', 1, 0, NULL) ");
            sb.Append("ON DUPLICATE KEY UPDATE `symbol` = VALUES(`symbol`), `address_code` = VALUES(`address_code`), `name` = VALUES(`name`), `val_type` = VALUES(`val_type`), `value_range` = VALUES(`value_range`), `default_val` = VALUES(`default_val`), `pid` = VALUES(`pid`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`); ");

            // Master
            sb.Append("INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (12, 'AOI', 'AOI', 0, NULL, 7, NULL, NULL, '2024-04-28 11:39:45', 1, 0, NULL, 0) ");
            sb.Append("ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`), `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);");

            return sb.ToString();
        }
    }
}