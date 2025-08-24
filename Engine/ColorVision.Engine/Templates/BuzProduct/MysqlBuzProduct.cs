using ColorVision.Database;

namespace ColorVision.Engine.Templates.BuzProduct
{
    public class MysqlBuzProduct : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复t_scgd_buz_product_master模板设置";
        public string GetRecover()
        {
            string sql = "CREATE TABLE `t_scgd_buz_product_master` ( `id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(255) DEFAULT NULL, `name` varchar(255) DEFAULT NULL, `buz_type` int(11) DEFAULT NULL COMMENT '类型', `cfg_json` json DEFAULT NULL COMMENT 'Json配置', `img_file` varchar(255) DEFAULT NULL COMMENT '产品图片文件', `create_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建日期', `is_enable` tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否可用，0-否/不可用1-是/可用', `is_delete` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否逻辑删除，0-否1-是', `tenant_id` int(11) DEFAULT NULL, `remark` varchar(255) DEFAULT NULL, PRIMARY KEY USING BTREE (`id`) ) ENGINE = InnoDB AUTO_INCREMENT = 7 CHARSET = utf8mb4 ROW_FORMAT = DYNAMIC;  CREATE TABLE `t_scgd_buz_product_detail` ( `id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(255) NOT NULL, `name` varchar(255) DEFAULT NULL, `pid` int(11) DEFAULT NULL, `poi_id` int(11) DEFAULT NULL COMMENT 'POI Id', `order_index` int(11) DEFAULT NULL COMMENT '排序索引', `cfg_json` json DEFAULT NULL COMMENT 'Json配置', `val_rule_temp_id` int(11) DEFAULT NULL COMMENT '合规判断模板ID', PRIMARY KEY USING BTREE (`id`) ) ENGINE = InnoDB AUTO_INCREMENT = 6 CHARSET = utf8mb4 ROW_FORMAT = DYNAMIC;";
            return sql;
        }
    }
}
