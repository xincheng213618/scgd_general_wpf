using ColorVision.Database;

namespace ColorVision.Engine.Services.PhyCameras.Dao
{
    public class MysqlCameraLicense : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复Mysql Camera设置";

        public string GetRecover()
        {
            string recover = "SET @column_exists = ( SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 't_scgd_camera_license' AND TABLE_SCHEMA = DATABASE() AND COLUMN_NAME = 'lic_type' );  IF @column_exists = 0 THEN ALTER TABLE `t_scgd_camera_license` ADD COLUMN `lic_type` int(11) NOT NULL DEFAULT '0' COMMENT 'license类型,0:相机，1-光谱仪'; END IF;";
            return recover;
        }
    }
}
