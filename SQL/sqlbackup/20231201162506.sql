/*
MySQL Backup
Database: cv
Backup Time: 2023-12-01 16:25:08
*/

SET FOREIGN_KEY_CHECKS=0;
DROP TABLE IF EXISTS `cv`.`t_scgd_algorithm_distortion_result`;
DROP TABLE IF EXISTS `cv`.`t_scgd_algorithm_focusPoints_result`;
DROP TABLE IF EXISTS `cv`.`t_scgd_algorithm_fov_result`;
DROP TABLE IF EXISTS `cv`.`t_scgd_algorithm_ghost_result`;
DROP TABLE IF EXISTS `cv`.`t_scgd_algorithm_ledcheck_result`;
DROP TABLE IF EXISTS `cv`.`t_scgd_algorithm_mtf_result`;
DROP TABLE IF EXISTS `cv`.`t_scgd_algorithm_poi_detail_result`;
DROP TABLE IF EXISTS `cv`.`t_scgd_algorithm_poi_master_result`;
DROP TABLE IF EXISTS `cv`.`t_scgd_algorithm_poi_template_detail`;
DROP TABLE IF EXISTS `cv`.`t_scgd_algorithm_poi_template_master`;
DROP TABLE IF EXISTS `cv`.`t_scgd_algorithm_result_detail_poi_mtf`;
DROP TABLE IF EXISTS `cv`.`t_scgd_algorithm_result_master`;
DROP TABLE IF EXISTS `cv`.`t_scgd_algorithm_sfr_result`;
DROP TABLE IF EXISTS `cv`.`t_scgd_license`;
DROP TABLE IF EXISTS `cv`.`t_scgd_measure_batch`;
DROP TABLE IF EXISTS `cv`.`t_scgd_measure_result_detail`;
DROP TABLE IF EXISTS `cv`.`t_scgd_measure_result_img`;
DROP TABLE IF EXISTS `cv`.`t_scgd_measure_result_smu`;
DROP TABLE IF EXISTS `cv`.`t_scgd_measure_result_smu_scan`;
DROP TABLE IF EXISTS `cv`.`t_scgd_measure_result_spectrometer`;
DROP TABLE IF EXISTS `cv`.`t_scgd_measure_template_detail`;
DROP TABLE IF EXISTS `cv`.`t_scgd_measure_template_master`;
DROP TABLE IF EXISTS `cv`.`t_scgd_mod_param_detail`;
DROP TABLE IF EXISTS `cv`.`t_scgd_mod_param_master`;
DROP TABLE IF EXISTS `cv`.`t_scgd_rc_app`;
DROP TABLE IF EXISTS `cv`.`t_scgd_rc_app_nodes`;
DROP TABLE IF EXISTS `cv`.`t_scgd_rc_sys_node`;
DROP TABLE IF EXISTS `cv`.`t_scgd_sys_camera`;
DROP TABLE IF EXISTS `cv`.`t_scgd_sys_dictionary`;
DROP TABLE IF EXISTS `cv`.`t_scgd_sys_dictionary_mod_item`;
DROP TABLE IF EXISTS `cv`.`t_scgd_sys_dictionary_mod_master`;
DROP TABLE IF EXISTS `cv`.`t_scgd_sys_mqtt_cfg`;
DROP TABLE IF EXISTS `cv`.`t_scgd_sys_resource`;
DROP TABLE IF EXISTS `cv`.`t_scgd_sys_tenant`;
DROP TABLE IF EXISTS `cv`.`t_scgd_sys_user`;
DROP TABLE IF EXISTS `cv`.`t_scgd_sys_user2tenant`;
DROP VIEW IF EXISTS `cv`.`v_scgd_algorithm_poi_detail_result`;
DROP VIEW IF EXISTS `cv`.`v_scgd_algorithm_poi_master_result`;
DROP VIEW IF EXISTS `cv`.`v_scgd_algorithm_result_master`;
DROP VIEW IF EXISTS `cv`.`v_scgd_measure_result_img`;
DROP VIEW IF EXISTS `cv`.`v_scgd_measure_template_detail_mod`;
DROP VIEW IF EXISTS `cv`.`v_scgd_mod_detail`;
DROP VIEW IF EXISTS `cv`.`v_scgd_mod_master`;
DROP VIEW IF EXISTS `cv`.`v_scgd_rc_sys_cfg`;
DROP VIEW IF EXISTS `cv`.`v_scgd_spectrometer_batch`;
DROP VIEW IF EXISTS `cv`.`v_scgd_spectrometer_batch_all`;
DROP VIEW IF EXISTS `cv`.`v_scgd_sys_dictionary`;
DROP VIEW IF EXISTS `cv`.`v_scgd_sys_dictionary_mod_item`;
DROP VIEW IF EXISTS `cv`.`v_scgd_sys_resource`;
DROP VIEW IF EXISTS `cv`.`v_scgd_sys_resource_algorithm`;
DROP VIEW IF EXISTS `cv`.`v_scgd_sys_resource_all`;
DROP VIEW IF EXISTS `cv`.`v_scgd_sys_resource_valid_all`;
DROP VIEW IF EXISTS `cv`.`v_scgd_sys_resource_valid_devices`;
DROP VIEW IF EXISTS `cv`.`v_scgd_sys_resource_valid_services`;
DROP PROCEDURE IF EXISTS `cv`.`pd_clear_deleted`;
CREATE TABLE `t_scgd_algorithm_distortion_result` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `batch_id` int(11) DEFAULT NULL COMMENT '批次号',
  `img_id` int(11) DEFAULT NULL,
  `value` text,
  `finalPointsX` text,
  `finalPointsY` text,
  `pointx` double DEFAULT NULL COMMENT '最大畸变点横坐标',
  `pointy` double DEFAULT NULL COMMENT '最大畸变点纵坐标',
  `maxErrorRatio` double DEFAULT NULL COMMENT '图像最大畸变率',
  `t` double DEFAULT NULL COMMENT '图像XOY方向的旋转角度',
  `ret` tinyint(1) DEFAULT NULL COMMENT '若执行成功返回true，否则false',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8;
CREATE TABLE `t_scgd_algorithm_focusPoints_result` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `batch_id` int(11) DEFAULT NULL,
  `img_id` int(11) DEFAULT NULL,
  `value` text,
  `imgPoints_x` text,
  `imgPoints_y` text,
  `ret` bigint(1) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `t_scgd_algorithm_fov_result` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `batch_id` int(11) DEFAULT NULL,
  `img_id` int(11) DEFAULT NULL,
  `value` text,
  `coordinates1` float DEFAULT NULL,
  `coordinates2` float DEFAULT NULL,
  `coordinates3` float DEFAULT NULL,
  `coordinates4` float DEFAULT NULL,
  `fovDegrees` double DEFAULT NULL,
  `ret` tinyint(1) NOT NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=42 DEFAULT CHARSET=latin1 ROW_FORMAT=DYNAMIC;
CREATE TABLE `t_scgd_algorithm_ghost_result` (
  `id` int(11) NOT NULL,
  `batch_id` int(11) DEFAULT NULL,
  `img_id` int(11) DEFAULT NULL,
  `value` text,
  `LedCenters_X` text,
  `LedCenters_Y` text,
  `blobGray` text,
  `ghostAverageGray` text,
  `singleLedPixelNum` text,
  `LED_pixel_X` text,
  `LED_pixel_Y` text,
  `singleGhostPixelNum` text,
  `Ghost_pixel_X` text,
  `Ghost_pixel_Y` text,
  `ret` tinyint(1) DEFAULT NULL COMMENT '若执行成功返回true，否则false',
  PRIMARY KEY (`id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=latin1 ROW_FORMAT=DYNAMIC;
CREATE TABLE `t_scgd_algorithm_ledcheck_result` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `batch_id` int(11) DEFAULT NULL,
  `img_id` int(11) DEFAULT NULL,
  `value` text,
  `databanjin` text,
  `datazuobiaoX` text,
  `datazuobiaoY` text,
  `PointX` text,
  `PointY` text,
  `LengthResult` text,
  `ret` tinyint(1) DEFAULT NULL COMMENT '若执行成功返回true，否则false',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `t_scgd_algorithm_mtf_result` (
  `id` int(11) NOT NULL,
  `batch_id` int(11) DEFAULT NULL,
  `img_id` int(11) DEFAULT NULL,
  `value` double DEFAULT NULL,
  `ret` tinyint(1) DEFAULT NULL COMMENT '若执行成功返回true，否则false',
  PRIMARY KEY (`id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=latin1 ROW_FORMAT=DYNAMIC;
CREATE TABLE `t_scgd_algorithm_poi_detail_result` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `pid` int(11) DEFAULT NULL,
  `poi_id` int(11) DEFAULT NULL,
  `poi_name` varchar(255) DEFAULT NULL,
  `poi_type` tinyint(2) DEFAULT NULL COMMENT '0:圆形;1:矩形',
  `poi_x` int(11) DEFAULT NULL,
  `poi_y` int(11) DEFAULT NULL,
  `poi_width` int(11) DEFAULT NULL,
  `poi_height` int(11) DEFAULT NULL,
  `value` text,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC;
CREATE TABLE `t_scgd_algorithm_poi_master_result` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `cie_file` varchar(255) DEFAULT NULL,
  `cie_file_type` tinyint(2) DEFAULT NULL COMMENT '0-色度;1-亮度',
  `pid` int(11) DEFAULT NULL,
  `batch_id` int(11) DEFAULT NULL,
  `result` varchar(255) DEFAULT NULL,
  `create_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC;
CREATE TABLE `t_scgd_algorithm_poi_template_detail` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL,
  `pid` int(11) DEFAULT NULL,
  `pt_type` tinyint(2) DEFAULT NULL COMMENT '0:圆形;1:矩形',
  `pix_x` int(11) DEFAULT NULL,
  `pix_y` int(11) DEFAULT NULL,
  `pix_width` int(11) DEFAULT NULL,
  `pix_height` int(11) DEFAULT NULL,
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  `is_delete` tinyint(1) NOT NULL DEFAULT '0',
  `remark` varchar(256) DEFAULT NULL,
  PRIMARY KEY (`id`) USING BTREE,
  KEY `idx_pid` (`pid`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=184 DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='关注点集';
CREATE TABLE `t_scgd_algorithm_poi_template_master` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL,
  `type` tinyint(2) DEFAULT '0' COMMENT '0:pixel;1:cad;2:灯珠',
  `width` int(11) DEFAULT '0',
  `height` int(11) DEFAULT '0',
  `left_top_x` int(11) DEFAULT '0',
  `left_top_y` int(11) DEFAULT '0',
  `right_top_x` int(11) DEFAULT '0',
  `right_top_y` int(11) DEFAULT '0',
  `right_bottom_x` int(11) DEFAULT '0',
  `right_bottom_y` int(11) DEFAULT '0',
  `left_bottom_x` int(11) DEFAULT '0',
  `left_bottom_y` int(11) DEFAULT '0',
  `cfg_json` text,
  `dynamics` tinyint(1) DEFAULT '0' COMMENT '0:false;1:true/动态抓取:灯珠下有效',
  `create_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  `is_delete` tinyint(1) NOT NULL DEFAULT '0',
  `tenant_id` int(11) DEFAULT NULL,
  `remark` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='关注点主表';
CREATE TABLE `t_scgd_algorithm_result_detail_poi_mtf` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `pid` int(11) DEFAULT NULL,
  `poi_id` int(11) DEFAULT NULL,
  `poi_name` varchar(255) DEFAULT NULL,
  `poi_type` tinyint(4) DEFAULT NULL COMMENT '0:圆形;1:矩形',
  `poi_x` int(11) DEFAULT NULL,
  `poi_y` int(11) DEFAULT NULL,
  `poi_width` int(11) DEFAULT NULL,
  `poi_height` int(11) DEFAULT NULL,
  `value` text,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC;
CREATE TABLE `t_scgd_algorithm_result_master` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `img_file` varchar(255) DEFAULT NULL,
  `img_file_type` tinyint(4) DEFAULT NULL COMMENT '0-色度;1-亮度;2-FOV;3-SFR;4-MTF;5-Ghost;6-Distortion;7-Calibration',
  `params` text COMMENT '参数',
  `tid` int(11) DEFAULT NULL COMMENT '参数模板ID',
  `tname` varchar(255) DEFAULT NULL COMMENT '参数模板名称',
  `batch_id` int(11) DEFAULT NULL,
  `device_code` varchar(255) DEFAULT NULL,
  `result_code` int(11) DEFAULT NULL,
  `result` varchar(255) DEFAULT NULL,
  `total_time` int(11) DEFAULT NULL COMMENT '总用时',
  `create_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC;
CREATE TABLE `t_scgd_algorithm_sfr_result` (
  `id` int(11) NOT NULL,
  `batch_id` int(11) DEFAULT NULL,
  `img_id` int(11) NOT NULL,
  `value` text,
  `pdfrequency` text COMMENT '空间频率分布',
  `pdomainSamplingData` text COMMENT '每像素周期数输出',
  `ret` tinyint(1) DEFAULT NULL COMMENT '若执行成功返回true，否则false',
  PRIMARY KEY (`id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=latin1 ROW_FORMAT=DYNAMIC;
CREATE TABLE `t_scgd_license` (
  `id` int(11) NOT NULL,
  `customer_name` varchar(64) NOT NULL DEFAULT '' COMMENT '客户名称',
  `mac_sn` varchar(255) DEFAULT NULL,
  `model` varchar(255) DEFAULT NULL,
  `value` text,
  `create_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  `is_delete` tinyint(1) NOT NULL DEFAULT '0',
  `remark` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `t_scgd_measure_batch` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `t_id` int(11) DEFAULT NULL COMMENT 't_scgd_measure_template_master',
  `name` varchar(255) DEFAULT NULL,
  `code` varchar(255) DEFAULT NULL,
  `create_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `total_time` int(11) DEFAULT NULL,
  `result` varchar(255) DEFAULT NULL,
  `tenant_id` int(11) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=89 DEFAULT CHARSET=utf8 COMMENT='测量批次主表';
CREATE TABLE `t_scgd_measure_result_detail` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `batch_id` int(11) DEFAULT NULL COMMENT 't_scgd_measure_batch',
  `img_id` int(11) DEFAULT NULL COMMENT 't_scgd_measure_value_img',
  `poi_did` int(11) DEFAULT NULL COMMENT '关注点t_scgd_cfg_poi_detail',
  `value` double DEFAULT NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='测量结果集';
CREATE TABLE `t_scgd_measure_result_img` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `batch_id` int(11) DEFAULT NULL,
  `params` text COMMENT '参数',
  `raw_file` varchar(255) DEFAULT NULL,
  `file_data` text CHARACTER SET utf8mb4 COLLATE utf8mb4_bin,
  `result_code` int(11) DEFAULT NULL,
  `result` varchar(255) DEFAULT NULL,
  `total_time` int(11) DEFAULT NULL COMMENT '总用时',
  `device_code` varchar(255) DEFAULT NULL,
  `create_date` datetime DEFAULT NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 ROW_FORMAT=DYNAMIC COMMENT='图像测量结果';
CREATE TABLE `t_scgd_measure_result_smu` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `pid` int(11) DEFAULT NULL,
  `batch_id` varchar(255) DEFAULT NULL COMMENT 'SN',
  `is_source_v` tinyint(1) DEFAULT NULL COMMENT '是否电压',
  `src_value` float DEFAULT NULL COMMENT '源值',
  `limit_value` float DEFAULT NULL COMMENT '限值',
  `v_result` float DEFAULT NULL COMMENT '电压',
  `i_result` float DEFAULT NULL COMMENT '电流',
  `create_date` datetime DEFAULT NULL,
  `device_code` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='源表测量结果';
CREATE TABLE `t_scgd_measure_result_smu_scan` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `pid` int(11) DEFAULT NULL,
  `batch_id` varchar(255) DEFAULT NULL COMMENT 'SN',
  `is_source_v` tinyint(1) DEFAULT NULL COMMENT '是否电压',
  `src_begin` float DEFAULT NULL COMMENT '开始源值',
  `src_end` float DEFAULT NULL COMMENT '结束源值',
  `points` int(11) DEFAULT NULL COMMENT '点数',
  `limit_value` float DEFAULT NULL COMMENT '限值',
  `v_result` json DEFAULT NULL COMMENT '电压',
  `i_result` json DEFAULT NULL COMMENT '电流',
  `create_date` datetime DEFAULT NULL,
  `device_code` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='源表测量结果';
CREATE TABLE `t_scgd_measure_result_spectrometer` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `fIntTime` float DEFAULT NULL COMMENT '积分时间',
  `iAveNum` int(11) DEFAULT NULL COMMENT '平均次数',
  `bUseAutoIntTime` tinyint(1) DEFAULT NULL COMMENT '自动积分',
  `bUseAutoDark` tinyint(1) DEFAULT NULL COMMENT '自适应校零',
  `pid` int(11) DEFAULT NULL,
  `batch_id` varchar(255) DEFAULT NULL,
  `fPL` json DEFAULT NULL COMMENT '相对光谱数据',
  `fRi` json DEFAULT NULL COMMENT '显色性指数 R1-R15',
  `fx` float DEFAULT NULL COMMENT '色坐标x',
  `fy` float DEFAULT NULL COMMENT '色坐标y',
  `fu` float DEFAULT NULL COMMENT '色坐标u',
  `fv` float DEFAULT NULL COMMENT '色坐标v',
  `fCCT` float DEFAULT NULL COMMENT '相关色温(K)',
  `dC` float DEFAULT NULL COMMENT '色差dC',
  `fLd` float DEFAULT NULL COMMENT '主波长(nm)',
  `fPur` float DEFAULT NULL COMMENT '色纯度(%)',
  `fLp` float DEFAULT NULL COMMENT '峰值波长(nm)',
  `fHW` float DEFAULT NULL COMMENT '半波宽(nm)',
  `fLav` float DEFAULT NULL COMMENT '平均波长(nm)',
  `fRa` float DEFAULT NULL COMMENT '显色性指数 Ra',
  `fRR` float DEFAULT NULL COMMENT '红色比',
  `fGR` float DEFAULT NULL COMMENT '绿色比',
  `fBR` float DEFAULT NULL COMMENT '蓝色比',
  `fIp` float DEFAULT NULL COMMENT '峰值AD',
  `fPh` float DEFAULT NULL COMMENT '光度值',
  `fPhe` float DEFAULT NULL COMMENT '辐射度值',
  `fPlambda` float DEFAULT NULL COMMENT '绝对光谱系数',
  `fSpect1` float DEFAULT NULL COMMENT '起始波长',
  `fSpect2` float DEFAULT NULL COMMENT '结束波长',
  `fInterval` float DEFAULT NULL COMMENT '波长间隔',
  `create_date` datetime DEFAULT NULL,
  `device_code` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='光谱仪测量结果';
CREATE TABLE `t_scgd_measure_template_detail` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `t_id` int(11) DEFAULT NULL,
  `t_type` tinyint(2) DEFAULT NULL COMMENT '0:POI;1:非POI(mod_master)',
  `pid` int(11) DEFAULT NULL,
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  `is_delete` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `t_scgd_measure_template_master` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL,
  `create_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  `is_delete` tinyint(1) NOT NULL DEFAULT '0',
  `remark` varchar(255) DEFAULT NULL,
  `tenant_id` int(11) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8;
CREATE TABLE `t_scgd_mod_param_detail` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `cc_pid` int(11) DEFAULT NULL COMMENT 't_scgd_sys_dictionary_mod_item',
  `value_a` varchar(64) DEFAULT NULL,
  `value_b` varchar(64) DEFAULT NULL,
  `pid` int(11) DEFAULT NULL COMMENT 't_scgd_mod_param_master',
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  `is_delete` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=125 DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='模板参数项表';
CREATE TABLE `t_scgd_mod_param_master` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(64) DEFAULT NULL,
  `mm_id` int(11) DEFAULT NULL COMMENT 't_scgd_sys_dictionary_mod_master',
  `mod_no` int(11) DEFAULT NULL,
  `create_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  `is_delete` tinyint(1) NOT NULL DEFAULT '0',
  `remark` varchar(256) DEFAULT NULL,
  `tenant_id` int(11) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='模板表';
CREATE TABLE `t_scgd_rc_app` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL,
  `app_id` varchar(255) DEFAULT NULL,
  `app_secret` varchar(255) DEFAULT NULL,
  `tenant_id` int(11) DEFAULT '0',
  `remark` varchar(255) DEFAULT NULL,
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  PRIMARY KEY (`id`),
  UNIQUE KEY `idx_appid` (`app_id`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8;
CREATE TABLE `t_scgd_rc_app_nodes` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `pid` int(11) DEFAULT NULL,
  `node_type` varchar(255) DEFAULT NULL,
  `node_name` varchar(255) DEFAULT NULL,
  `node_services` varchar(512) DEFAULT NULL,
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=14 DEFAULT CHARSET=utf8;
CREATE TABLE `t_scgd_rc_sys_node` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL COMMENT '节点名称',
  `rc_name` varchar(255) DEFAULT NULL COMMENT 'RC名称',
  `type_codes` varchar(512) DEFAULT NULL COMMENT '节点类型',
  `app_id` varchar(255) DEFAULT NULL COMMENT 'App ID',
  `app_key` varchar(255) DEFAULT NULL COMMENT 'App Key',
  `tenant_id` int(11) DEFAULT '0' COMMENT '租户ID',
  `is_enable` tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否可用，0-否/不可用;1-是/可用',
  `is_delete` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否删除，0-否;1-是',
  `mqtt_cfg_id` int(11) DEFAULT NULL COMMENT 'MQTT配置ID',
  `create_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP COMMENT '创建日期',
  `remark` varchar(255) DEFAULT NULL COMMENT '备注',
  PRIMARY KEY (`id`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC;
CREATE TABLE `t_scgd_sys_camera` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `SnID` text,
  `Value` text,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `t_scgd_sys_dictionary` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL,
  `pid` int(11) DEFAULT NULL,
  `key` varchar(255) DEFAULT NULL,
  `val` int(11) DEFAULT NULL,
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  `is_delete` tinyint(1) NOT NULL DEFAULT '0',
  `remark` varchar(255) DEFAULT NULL,
  `tenant_id` int(11) DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `idx_val` (`val`)
) ENGINE=InnoDB AUTO_INCREMENT=38 DEFAULT CHARSET=utf8 COMMENT='系统字典表';
CREATE TABLE `t_scgd_sys_dictionary_mod_item` (
  `id` int(11) NOT NULL,
  `symbol` varchar(32) DEFAULT NULL,
  `address_code` bigint(20) DEFAULT NULL,
  `name` varchar(64) DEFAULT NULL,
  `val_type` tinyint(2) DEFAULT NULL COMMENT '0:整数;1:浮点;2:布尔;3:字符串;4:枚举',
  `value_range` varchar(64) DEFAULT NULL,
  `default_val` varchar(64) DEFAULT NULL,
  `pid` int(11) DEFAULT NULL COMMENT 't_scgd_sys_dictionary_mod_master',
  `create_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  `is_delete` tinyint(1) NOT NULL DEFAULT '0',
  `remark` varchar(256) DEFAULT NULL,
  PRIMARY KEY (`id`) USING BTREE,
  KEY `idx_pid` (`pid`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='模块参数项字典';
CREATE TABLE `t_scgd_sys_dictionary_mod_master` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `code` varchar(16) DEFAULT NULL,
  `name` varchar(64) DEFAULT NULL,
  `pid` int(11) DEFAULT NULL,
  `create_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  `is_delete` tinyint(1) NOT NULL DEFAULT '0',
  `remark` varchar(256) DEFAULT NULL,
  `tenant_id` int(11) DEFAULT '0',
  PRIMARY KEY (`id`) USING BTREE,
  KEY `idx_code` (`code`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=17 DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='模块字典主表';
CREATE TABLE `t_scgd_sys_mqtt_cfg` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL,
  `host` varchar(255) DEFAULT NULL,
  `port` int(11) DEFAULT NULL COMMENT '端口',
  `user` varchar(255) DEFAULT NULL,
  `password` varchar(255) DEFAULT NULL,
  `endpoint` varchar(255) DEFAULT NULL,
  `create_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `tenant_id` int(11) DEFAULT '0',
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  `is_delete` tinyint(1) NOT NULL DEFAULT '0',
  `remark` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8;
CREATE TABLE `t_scgd_sys_resource` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL,
  `code` varchar(255) DEFAULT NULL,
  `type` int(11) DEFAULT NULL,
  `pid` int(11) DEFAULT NULL,
  `value` longblob,
  `txt_value` longtext,
  `create_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `tenant_id` int(11) DEFAULT '0',
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  `is_delete` tinyint(1) NOT NULL DEFAULT '0',
  `remark` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=66 DEFAULT CHARSET=utf8;
CREATE TABLE `t_scgd_sys_tenant` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL,
  `create_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  `is_delete` tinyint(1) NOT NULL DEFAULT '0',
  `remark` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8 COMMENT='租户表';
CREATE TABLE `t_scgd_sys_user` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL,
  `code` varchar(255) DEFAULT NULL,
  `pwd` varchar(255) DEFAULT NULL,
  `create_date` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `is_enable` tinyint(1) NOT NULL DEFAULT '1',
  `is_delete` tinyint(1) NOT NULL DEFAULT '0',
  `remark` varchar(256) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8 COMMENT='用户表';
CREATE TABLE `t_scgd_sys_user2tenant` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `user_id` int(11) DEFAULT NULL,
  `tenant_id` int(11) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8 COMMENT='用户和租户关系表';
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_algorithm_poi_detail_result` AS select `t_scgd_algorithm_poi_detail_result`.`id` AS `id`,`t_scgd_algorithm_poi_detail_result`.`pid` AS `pid`,`t_scgd_algorithm_poi_detail_result`.`poi_id` AS `poi_id`,`t_scgd_algorithm_poi_detail_result`.`poi_name` AS `poi_name`,`t_scgd_algorithm_poi_detail_result`.`poi_type` AS `poi_type`,`t_scgd_algorithm_poi_detail_result`.`poi_x` AS `poi_x`,`t_scgd_algorithm_poi_detail_result`.`poi_y` AS `poi_y`,`t_scgd_algorithm_poi_detail_result`.`poi_width` AS `poi_width`,`t_scgd_algorithm_poi_detail_result`.`poi_height` AS `poi_height`,`t_scgd_algorithm_poi_detail_result`.`value` AS `value`,`t_scgd_algorithm_poi_master_result`.`batch_id` AS `batch_id`,`t_scgd_measure_batch`.`code` AS `batch_code` from ((`t_scgd_algorithm_poi_detail_result` left join `t_scgd_algorithm_poi_master_result` on((`t_scgd_algorithm_poi_detail_result`.`pid` = `t_scgd_algorithm_poi_master_result`.`id`))) left join `t_scgd_measure_batch` on((`t_scgd_algorithm_poi_master_result`.`batch_id` = `t_scgd_measure_batch`.`id`)));
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_algorithm_poi_master_result` AS select `t_scgd_algorithm_poi_master_result`.`id` AS `id`,`t_scgd_algorithm_poi_master_result`.`cie_file` AS `cie_file`,`t_scgd_algorithm_poi_master_result`.`cie_file_type` AS `cie_file_type`,`t_scgd_algorithm_poi_master_result`.`pid` AS `pid`,`t_scgd_algorithm_poi_template_master`.`name` AS `pname`,`t_scgd_algorithm_poi_master_result`.`batch_id` AS `batch_id`,`t_scgd_algorithm_poi_master_result`.`result` AS `result`,`t_scgd_algorithm_poi_master_result`.`create_date` AS `create_date` from (`t_scgd_algorithm_poi_master_result` left join `t_scgd_algorithm_poi_template_master` on((`t_scgd_algorithm_poi_master_result`.`pid` = `t_scgd_algorithm_poi_template_master`.`id`))) order by `t_scgd_algorithm_poi_master_result`.`id`;
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_algorithm_result_master` AS select `t_scgd_algorithm_result_master`.`id` AS `id`,`t_scgd_algorithm_result_master`.`img_file` AS `img_file`,`t_scgd_algorithm_result_master`.`img_file_type` AS `img_file_type`,`t_scgd_algorithm_result_master`.`params` AS `params`,`t_scgd_algorithm_result_master`.`tid` AS `tid`,`t_scgd_algorithm_result_master`.`tname` AS `tname`,`t_scgd_algorithm_result_master`.`batch_id` AS `batch_id`,`t_scgd_algorithm_result_master`.`device_code` AS `device_code`,`v_scgd_sys_resource_algorithm`.`name` AS `device_name`,`t_scgd_algorithm_result_master`.`result_code` AS `result_code`,`t_scgd_algorithm_result_master`.`result` AS `result`,`t_scgd_algorithm_result_master`.`total_time` AS `total_time`,`t_scgd_algorithm_result_master`.`create_date` AS `create_date`,`t_scgd_measure_batch`.`code` AS `batch_code` from ((`t_scgd_algorithm_result_master` left join `t_scgd_measure_batch` on((`t_scgd_algorithm_result_master`.`batch_id` = `t_scgd_measure_batch`.`id`))) left join `v_scgd_sys_resource_algorithm` on((`t_scgd_algorithm_result_master`.`device_code` = `v_scgd_sys_resource_algorithm`.`code`)));
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_measure_result_img` AS select `t_scgd_measure_result_img`.`id` AS `id`,`t_scgd_measure_result_img`.`batch_id` AS `batch_id`,`t_scgd_measure_batch`.`code` AS `batch_code`,`t_scgd_measure_result_img`.`params` AS `params`,`t_scgd_measure_result_img`.`raw_file` AS `raw_file`,`t_scgd_measure_result_img`.`file_data` AS `file_data`,`t_scgd_measure_result_img`.`result_code` AS `result_code`,`t_scgd_measure_result_img`.`result` AS `result`,`t_scgd_measure_result_img`.`total_time` AS `total_time`,`t_scgd_measure_result_img`.`device_code` AS `device_code`,`t_scgd_measure_result_img`.`create_date` AS `create_date` from (`t_scgd_measure_result_img` left join `t_scgd_measure_batch` on((`t_scgd_measure_result_img`.`batch_id` = `t_scgd_measure_batch`.`id`)));
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_measure_template_detail_mod` AS select `t_scgd_measure_template_detail`.`id` AS `id`,`t_scgd_measure_template_detail`.`t_id` AS `t_id`,`t_scgd_measure_template_detail`.`t_type` AS `t_type`,`t_scgd_measure_template_detail`.`pid` AS `pid`,`t_scgd_measure_template_detail`.`is_enable` AS `is_enable`,`t_scgd_measure_template_detail`.`is_delete` AS `is_delete`,`v_scgd_mod_master`.`name` AS `name`,`v_scgd_mod_master`.`pcode` AS `pcode`,`v_scgd_mod_master`.`pname` AS `pname` from (`t_scgd_measure_template_detail` join `v_scgd_mod_master` on((`t_scgd_measure_template_detail`.`t_id` = `v_scgd_mod_master`.`id`))) where (`t_scgd_measure_template_detail`.`t_type` = 1);
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_mod_detail` AS select `t_scgd_mod_param_detail`.`id` AS `id`,`t_scgd_mod_param_detail`.`cc_pid` AS `cc_pid`,`t_scgd_mod_param_detail`.`value_a` AS `value_a`,`t_scgd_mod_param_detail`.`value_b` AS `value_b`,`t_scgd_mod_param_detail`.`pid` AS `pid`,`t_scgd_mod_param_detail`.`is_enable` AS `is_enable`,`t_scgd_mod_param_detail`.`is_delete` AS `is_delete`,`t_scgd_sys_dictionary_mod_item`.`symbol` AS `symbol`,`t_scgd_sys_dictionary_mod_item`.`name` AS `name`,`v_scgd_mod_master`.`name` AS `MName`,`v_scgd_mod_master`.`pcode` AS `pcode`,`t_scgd_sys_dictionary_mod_item`.`val_type` AS `val_type` from ((`t_scgd_mod_param_detail` join `t_scgd_sys_dictionary_mod_item` on((`t_scgd_mod_param_detail`.`cc_pid` = `t_scgd_sys_dictionary_mod_item`.`id`))) join `v_scgd_mod_master` on((`t_scgd_mod_param_detail`.`pid` = `v_scgd_mod_master`.`id`))) where (`t_scgd_mod_param_detail`.`is_delete` = 0);
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_mod_master` AS select `t_scgd_mod_param_master`.`id` AS `id`,`t_scgd_mod_param_master`.`name` AS `name`,`t_scgd_mod_param_master`.`mm_id` AS `pid`,`t_scgd_mod_param_master`.`mod_no` AS `mod_no`,`t_scgd_mod_param_master`.`create_date` AS `create_date`,`t_scgd_mod_param_master`.`tenant_id` AS `tenant_id`,`t_scgd_sys_dictionary_mod_master`.`code` AS `pcode`,`t_scgd_sys_dictionary_mod_master`.`name` AS `pname`,`t_scgd_mod_param_master`.`is_enable` AS `is_enable`,`t_scgd_mod_param_master`.`is_delete` AS `is_delete`,`t_scgd_mod_param_master`.`remark` AS `remark` from (`t_scgd_mod_param_master` join `t_scgd_sys_dictionary_mod_master` on((`t_scgd_mod_param_master`.`mm_id` = `t_scgd_sys_dictionary_mod_master`.`id`))) where (`t_scgd_mod_param_master`.`is_delete` = 0);
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_rc_sys_cfg` AS select `t_scgd_rc_sys_node`.`id` AS `id`,`t_scgd_rc_sys_node`.`name` AS `name`,`t_scgd_rc_sys_node`.`rc_name` AS `rc_name`,`t_scgd_rc_sys_node`.`type_codes` AS `type_codes`,`t_scgd_rc_sys_node`.`app_id` AS `app_id`,`t_scgd_rc_sys_node`.`app_key` AS `app_key`,`t_scgd_rc_sys_node`.`tenant_id` AS `tenant_id`,`t_scgd_rc_sys_node`.`is_enable` AS `is_enable`,`t_scgd_rc_sys_node`.`is_delete` AS `is_delete`,`t_scgd_rc_sys_node`.`mqtt_cfg_id` AS `mqtt_cfg_id`,`t_scgd_rc_sys_node`.`create_date` AS `create_date`,`t_scgd_rc_sys_node`.`remark` AS `remark`,`t_scgd_sys_mqtt_cfg`.`host` AS `mqtt_host`,`t_scgd_sys_mqtt_cfg`.`port` AS `mqtt_port`,`t_scgd_sys_mqtt_cfg`.`user` AS `mqtt_user`,`t_scgd_sys_mqtt_cfg`.`password` AS `mqtt_pwd` from (`t_scgd_rc_sys_node` left join `t_scgd_sys_mqtt_cfg` on((`t_scgd_rc_sys_node`.`mqtt_cfg_id` = `t_scgd_sys_mqtt_cfg`.`id`)));
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_spectrometer_batch` AS select `v_scgd_spectrometer_batch_all`.`batch_id` AS `batch_id`,`v_scgd_spectrometer_batch_all`.`create_date` AS `create_date` from `v_scgd_spectrometer_batch_all` where (`v_scgd_spectrometer_batch_all`.`batch_id` is not null) union select `v_scgd_spectrometer_batch_all`.`code` AS `code`,`v_scgd_spectrometer_batch_all`.`batch_create_date` AS `create_date` from `v_scgd_spectrometer_batch_all` where (`v_scgd_spectrometer_batch_all`.`code` is not null);
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_spectrometer_batch_all` AS select `t_scgd_measure_result_spectrometer`.`id` AS `id`,`t_scgd_measure_result_spectrometer`.`batch_id` AS `batch_id`,`t_scgd_measure_result_spectrometer`.`create_date` AS `create_date`,`t_scgd_measure_batch`.`code` AS `code`,`t_scgd_measure_batch`.`create_date` AS `batch_create_date` from (`t_scgd_measure_result_spectrometer` left join `t_scgd_measure_batch` on((`t_scgd_measure_result_spectrometer`.`pid` = `t_scgd_measure_batch`.`id`)));
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_sys_dictionary` AS select `a`.`id` AS `id`,`a`.`name` AS `name`,`a`.`key` AS `code`,`a`.`pid` AS `pid`,`a`.`val` AS `val`,`b`.`key` AS `pcode`,`b`.`name` AS `pname`,`a`.`remark` AS `remark`,`a`.`tenant_id` AS `tenant_id`,`a`.`is_enable` AS `is_enable`,`a`.`is_delete` AS `is_delete` from (`t_scgd_sys_dictionary` `a` join `t_scgd_sys_dictionary` `b` on((`a`.`pid` = `b`.`id`))) where (`a`.`pid` > 0);
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_sys_dictionary_mod_item` AS select `t_scgd_sys_dictionary_mod_item`.`id` AS `id`,`t_scgd_sys_dictionary_mod_item`.`symbol` AS `symbol`,`t_scgd_sys_dictionary_mod_item`.`address_code` AS `address_code`,`t_scgd_sys_dictionary_mod_item`.`name` AS `name`,`t_scgd_sys_dictionary_mod_item`.`val_type` AS `val_type`,`t_scgd_sys_dictionary_mod_item`.`value_range` AS `value_range`,`t_scgd_sys_dictionary_mod_item`.`default_val` AS `default_val`,`t_scgd_sys_dictionary_mod_item`.`pid` AS `pid`,`t_scgd_sys_dictionary_mod_master`.`code` AS `pcode`,`t_scgd_sys_dictionary_mod_item`.`create_date` AS `create_date`,`t_scgd_sys_dictionary_mod_item`.`is_enable` AS `is_enable`,`t_scgd_sys_dictionary_mod_item`.`is_delete` AS `is_delete`,`t_scgd_sys_dictionary_mod_item`.`remark` AS `remark` from (`t_scgd_sys_dictionary_mod_item` join `t_scgd_sys_dictionary_mod_master` on((`t_scgd_sys_dictionary_mod_item`.`pid` = `t_scgd_sys_dictionary_mod_master`.`id`)));
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_sys_resource` AS select `t_scgd_sys_resource`.`id` AS `id`,`t_scgd_sys_resource`.`name` AS `name`,`t_scgd_sys_resource`.`code` AS `code`,`t_scgd_sys_resource`.`pid` AS `pid`,`t_scgd_sys_resource`.`type` AS `type`,`v_scgd_sys_dictionary`.`code` AS `type_code`,`v_scgd_sys_dictionary`.`name` AS `type_name`,`t_scgd_sys_resource`.`value` AS `value`,`t_scgd_sys_resource`.`txt_value` AS `txt_value`,`t_scgd_sys_resource`.`create_date` AS `create_date`,`t_scgd_sys_resource`.`tenant_id` AS `tenant_id`,`t_scgd_sys_resource`.`is_enable` AS `is_enable`,`t_scgd_sys_resource`.`is_delete` AS `is_delete`,`t_scgd_sys_resource`.`remark` AS `remark`,`v_scgd_sys_dictionary`.`pid` AS `ppid`,`v_scgd_sys_dictionary`.`pcode` AS `ppcode`,`v_scgd_sys_dictionary`.`pname` AS `ppname`,`v_scgd_sys_dictionary`.`remark` AS `assembly_qualified_name` from (`t_scgd_sys_resource` join `v_scgd_sys_dictionary` on((`t_scgd_sys_resource`.`type` = `v_scgd_sys_dictionary`.`val`)));
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_sys_resource_algorithm` AS select `t_scgd_sys_resource`.`id` AS `id`,`t_scgd_sys_resource`.`name` AS `name`,`t_scgd_sys_resource`.`code` AS `code`,`t_scgd_sys_resource`.`type` AS `type`,`t_scgd_sys_resource`.`pid` AS `pid`,`t_scgd_sys_resource`.`value` AS `value`,`t_scgd_sys_resource`.`txt_value` AS `txt_value`,`t_scgd_sys_resource`.`create_date` AS `create_date`,`t_scgd_sys_resource`.`tenant_id` AS `tenant_id`,`t_scgd_sys_resource`.`is_enable` AS `is_enable`,`t_scgd_sys_resource`.`is_delete` AS `is_delete`,`t_scgd_sys_resource`.`remark` AS `remark` from `t_scgd_sys_resource` where ((`t_scgd_sys_resource`.`type` = 7) and (`t_scgd_sys_resource`.`pid` is not null));
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_sys_resource_all` AS select `v1`.`id` AS `id`,`v1`.`name` AS `name`,`v1`.`code` AS `code`,`v1`.`pid` AS `pid`,`v2`.`code` AS `pcode`,`v1`.`type` AS `type`,`v1`.`type_code` AS `type_code`,`v1`.`type_name` AS `type_name`,`v1`.`value` AS `value`,`v1`.`txt_value` AS `txt_value`,`v1`.`create_date` AS `create_date`,`v1`.`tenant_id` AS `tenant_id`,`v1`.`is_enable` AS `is_enable`,`v1`.`is_delete` AS `is_delete`,`v1`.`remark` AS `remark`,`v1`.`ppid` AS `ppid`,`v1`.`ppcode` AS `ppcode`,`v1`.`ppname` AS `ppname`,`v1`.`assembly_qualified_name` AS `assembly_qualified_name` from (`v_scgd_sys_resource` `v1` left join `v_scgd_sys_resource` `v2` on((`v1`.`pid` = `v2`.`id`)));
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_sys_resource_valid_all` AS select `v_scgd_sys_resource_all`.`id` AS `id`,`v_scgd_sys_resource_all`.`name` AS `name`,`v_scgd_sys_resource_all`.`code` AS `code`,`v_scgd_sys_resource_all`.`pid` AS `pid`,`v_scgd_sys_resource_all`.`pcode` AS `pcode`,`v_scgd_sys_resource_all`.`type` AS `type`,`v_scgd_sys_resource_all`.`type_code` AS `type_code`,`v_scgd_sys_resource_all`.`type_name` AS `type_name`,`v_scgd_sys_resource_all`.`value` AS `value`,`v_scgd_sys_resource_all`.`txt_value` AS `txt_value`,`v_scgd_sys_resource_all`.`create_date` AS `create_date`,`v_scgd_sys_resource_all`.`tenant_id` AS `tenant_id`,`v_scgd_sys_resource_all`.`is_enable` AS `is_enable`,`v_scgd_sys_resource_all`.`is_delete` AS `is_delete`,`v_scgd_sys_resource_all`.`remark` AS `remark`,`v_scgd_sys_resource_all`.`ppid` AS `ppid`,`v_scgd_sys_resource_all`.`ppcode` AS `ppcode`,`v_scgd_sys_resource_all`.`ppname` AS `ppname`,`v_scgd_sys_resource_all`.`assembly_qualified_name` AS `assembly_qualified_name` from `v_scgd_sys_resource_all` where ((`v_scgd_sys_resource_all`.`is_delete` = 0) and (`v_scgd_sys_resource_all`.`is_enable` = 1));
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_sys_resource_valid_devices` AS select `v_scgd_sys_resource_valid_all`.`id` AS `id`,`v_scgd_sys_resource_valid_all`.`name` AS `name`,`v_scgd_sys_resource_valid_all`.`code` AS `code`,`v_scgd_sys_resource_valid_all`.`pid` AS `pid`,`v_scgd_sys_resource_valid_all`.`pcode` AS `pcode`,`v_scgd_sys_resource_valid_all`.`type` AS `type`,`v_scgd_sys_resource_valid_all`.`type_code` AS `type_code`,`v_scgd_sys_resource_valid_all`.`type_name` AS `type_name`,`v_scgd_sys_resource_valid_all`.`value` AS `value`,`v_scgd_sys_resource_valid_all`.`txt_value` AS `txt_value`,`v_scgd_sys_resource_valid_all`.`create_date` AS `create_date`,`v_scgd_sys_resource_valid_all`.`tenant_id` AS `tenant_id`,`v_scgd_sys_resource_valid_all`.`is_enable` AS `is_enable`,`v_scgd_sys_resource_valid_all`.`is_delete` AS `is_delete`,`v_scgd_sys_resource_valid_all`.`remark` AS `remark`,`v_scgd_sys_resource_valid_all`.`ppid` AS `ppid`,`v_scgd_sys_resource_valid_all`.`ppcode` AS `ppcode`,`v_scgd_sys_resource_valid_all`.`ppname` AS `ppname`,`v_scgd_sys_resource_valid_all`.`assembly_qualified_name` AS `assembly_qualified_name` from `v_scgd_sys_resource_valid_all` where ((`v_scgd_sys_resource_valid_all`.`pid` is not null) and (`v_scgd_sys_resource_valid_all`.`pid` > 0));
CREATE ALGORITHM=UNDEFINED DEFINER=`root`@`%` SQL SECURITY DEFINER VIEW `v_scgd_sys_resource_valid_services` AS select `v_scgd_sys_resource_valid_all`.`id` AS `id`,`v_scgd_sys_resource_valid_all`.`name` AS `name`,`v_scgd_sys_resource_valid_all`.`code` AS `code`,`v_scgd_sys_resource_valid_all`.`pid` AS `pid`,`v_scgd_sys_resource_valid_all`.`pcode` AS `pcode`,`v_scgd_sys_resource_valid_all`.`type` AS `type`,`v_scgd_sys_resource_valid_all`.`type_code` AS `type_code`,`v_scgd_sys_resource_valid_all`.`type_name` AS `type_name`,`v_scgd_sys_resource_valid_all`.`value` AS `value`,`v_scgd_sys_resource_valid_all`.`txt_value` AS `txt_value`,`v_scgd_sys_resource_valid_all`.`create_date` AS `create_date`,`v_scgd_sys_resource_valid_all`.`tenant_id` AS `tenant_id`,`v_scgd_sys_resource_valid_all`.`is_enable` AS `is_enable`,`v_scgd_sys_resource_valid_all`.`is_delete` AS `is_delete`,`v_scgd_sys_resource_valid_all`.`remark` AS `remark`,`v_scgd_sys_resource_valid_all`.`ppid` AS `ppid`,`v_scgd_sys_resource_valid_all`.`ppcode` AS `ppcode`,`v_scgd_sys_resource_valid_all`.`ppname` AS `ppname`,`v_scgd_sys_resource_valid_all`.`assembly_qualified_name` AS `assembly_qualified_name` from `v_scgd_sys_resource_valid_all` where (isnull(`v_scgd_sys_resource_valid_all`.`pid`) or (`v_scgd_sys_resource_valid_all`.`pid` = -(1)));
CREATE DEFINER=`root`@`%` PROCEDURE `pd_clear_deleted`()
BEGIN
	#Routine body goes here...
  DELETE FROM t_scgd_sys_resource WHERE is_delete = 1;
	DELETE FROM t_scgd_mod_param_master WHERE is_delete = 1;
	DELETE FROM t_scgd_mod_param_detail WHERE is_delete = 1;
	DELETE FROM t_scgd_algorithm_poi_template_detail WHERE is_delete = 1;
	DELETE FROM t_scgd_algorithm_poi_template_detail WHERE is_delete = 1;
	DELETE FROM t_scgd_measure_template_detail WHERE is_delete = 1;
	DELETE FROM t_scgd_measure_template_master WHERE is_delete = 1;
END;
BEGIN;
LOCK TABLES `cv`.`t_scgd_algorithm_distortion_result` WRITE;
DELETE FROM `cv`.`t_scgd_algorithm_distortion_result`;
INSERT INTO `cv`.`t_scgd_algorithm_distortion_result` (`id`,`batch_id`,`img_id`,`value`,`finalPointsX`,`finalPointsY`,`pointx`,`pointy`,`maxErrorRatio`,`t`,`ret`) VALUES (1, 178, 1, '0', '1', '11', 1, 1, 1, 53, 1),(2, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_algorithm_focusPoints_result` WRITE;
DELETE FROM `cv`.`t_scgd_algorithm_focusPoints_result`;
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_algorithm_fov_result` WRITE;
DELETE FROM `cv`.`t_scgd_algorithm_fov_result`;
INSERT INTO `cv`.`t_scgd_algorithm_fov_result` (`id`,`batch_id`,`img_id`,`value`,`coordinates1`,`coordinates2`,`coordinates3`,`coordinates4`,`fovDegrees`,`ret`) VALUES (9, -1, 1, '', -431602000, -431602000, -431602000, -431602000, 75.6286870162306, 1),(10, -1, 1, '', -431602000, -431602000, -431602000, -431602000, 75.6286870162306, 1),(11, -1, 1, '', -431602000, -431602000, -431602000, -431602000, 70.4554436132062, 1),(12, -1, 1, '', -431602000, -431602000, -431602000, -431602000, 75.6286870162306, 1),(13, -1, 1, '', -431602000, -431602000, -431602000, -431602000, 75.6286870162306, 1),(14, 1, 2, '0', 0, 0, 0, 0, 0, 1),(15, 298, 1, '0', 0, 0, 0, 0, 0, 0),(16, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0, 0),(17, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0, 0),(18, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0, 0),(19, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0, 0),(20, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0, 0),(21, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0, 0),(22, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0, 0),(23, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0, 0),(24, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0, 0),(25, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0, 0),(26, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 70.2340378232021, 1),(27, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 70.2340378232021, 1),(28, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 70.2340378232021, 1),(29, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 2.10189279429872, 1),(30, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0.131395625986626, 1),(31, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0.172439608643286, 1),(32, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0.172439608643286, 1),(33, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0.172439608643286, 1),(34, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0.131395625986626, 1),(35, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0.131395625986626, 1),(36, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0.131395625986626, 1),(37, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0.131395625986626, 1),(38, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0.131395625986626, 1),(39, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0.0108683296489344, 1),(40, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0.0108683296489344, 1),(41, -1, -1, '', -431602000, -431602000, -431602000, -431602000, 0.131395625986626, 1);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_algorithm_ghost_result` WRITE;
DELETE FROM `cv`.`t_scgd_algorithm_ghost_result`;
INSERT INTO `cv`.`t_scgd_algorithm_ghost_result` (`id`,`batch_id`,`img_id`,`value`,`LedCenters_X`,`LedCenters_Y`,`blobGray`,`ghostAverageGray`,`singleLedPixelNum`,`LED_pixel_X`,`LED_pixel_Y`,`singleGhostPixelNum`,`Ghost_pixel_X`,`Ghost_pixel_Y`,`ret`) VALUES (1, 179, 2, '2', '5', '45', '456', '456', '456', '546', '546', '456', '56', '46', 1);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_algorithm_ledcheck_result` WRITE;
DELETE FROM `cv`.`t_scgd_algorithm_ledcheck_result`;
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_algorithm_mtf_result` WRITE;
DELETE FROM `cv`.`t_scgd_algorithm_mtf_result`;
INSERT INTO `cv`.`t_scgd_algorithm_mtf_result` (`id`,`batch_id`,`img_id`,`value`,`ret`) VALUES (1, 45, 456, 456, 1);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_algorithm_poi_detail_result` WRITE;
DELETE FROM `cv`.`t_scgd_algorithm_poi_detail_result`;
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_algorithm_poi_master_result` WRITE;
DELETE FROM `cv`.`t_scgd_algorithm_poi_master_result`;
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_algorithm_poi_template_detail` WRITE;
DELETE FROM `cv`.`t_scgd_algorithm_poi_template_detail`;
INSERT INTO `cv`.`t_scgd_algorithm_poi_template_detail` (`id`,`name`,`pid`,`pt_type`,`pix_x`,`pix_y`,`pix_width`,`pix_height`,`is_enable`,`is_delete`,`remark`) VALUES (1, NULL, 2, 0, 5000, 2000, 10, 10, 0, 0, NULL),(2, NULL, 2, 0, 4000, 3732, 10, 10, 0, 0, NULL),(3, NULL, 2, 0, 2000, 3732, 10, 10, 0, 0, NULL),(4, NULL, 2, 0, 1000, 2000, 10, 10, 0, 0, NULL),(5, NULL, 2, 0, 1999, 267, 10, 10, 0, 0, NULL),(6, NULL, 2, 0, 4000, 267, 10, 10, 0, 0, NULL),(8, NULL, 1, 0, 7933, 3748, 1010, 1010, 0, 0, NULL),(9, NULL, 1, 0, 7006, 5491, 1010, 1010, 0, 0, NULL),(10, NULL, 1, 0, 5232, 6356, 1010, 1010, 0, 0, NULL),(11, NULL, 1, 0, 3288, 6014, 1010, 1010, 0, 0, NULL),(12, NULL, 1, 0, 1917, 4594, 1010, 1010, 0, 0, NULL),(13, NULL, 1, 0, 1642, 2639, 1010, 1010, 0, 0, NULL),(14, NULL, 1, 0, 2569, 896, 1010, 1010, 0, 0, NULL),(15, NULL, 1, 0, 4343, 31, 1010, 1010, 0, 0, NULL),(16, NULL, 1, 0, 6287, 373, 1010, 1010, 0, 0, NULL),(17, NULL, 1, 0, 7658, 1793, 1010, 1010, 0, 0, NULL),(18, NULL, 1, 0, 0, 0, 1010, 1010, 0, 0, NULL),(19, NULL, 1, 0, 4788, 0, 1010, 1010, 0, 0, NULL),(20, NULL, 1, 0, 9576, 0, 1010, 1010, 0, 0, NULL),(21, NULL, 1, 0, 0, 3194, 1010, 1010, 0, 0, NULL),(22, NULL, 1, 0, 4788, 3194, 1010, 1010, 0, 0, NULL),(23, NULL, 1, 0, 9576, 3194, 1010, 1010, 0, 0, NULL),(24, NULL, 1, 0, 0, 6388, 1010, 1010, 0, 0, NULL),(25, NULL, 1, 0, 4788, 6388, 1010, 1010, 0, 0, NULL),(26, NULL, 1, 0, 9576, 6388, 1010, 1010, 0, 0, NULL),(39, NULL, 3, 0, 0, 0, 10, 10, 0, 0, NULL),(40, NULL, 3, 0, 615, 0, 10, 10, 0, 0, NULL),(41, NULL, 3, 0, 1231, 0, 10, 10, 0, 0, NULL),(42, NULL, 3, 0, 1846, 0, 10, 10, 0, 0, NULL),(43, NULL, 3, 0, 2462, 0, 10, 10, 0, 0, NULL),(44, NULL, 3, 0, 3077, 0, 10, 10, 0, 0, NULL),(45, NULL, 3, 0, 3693, 0, 10, 10, 0, 0, NULL),(46, NULL, 3, 0, 4308, 0, 10, 10, 0, 0, NULL),(47, NULL, 3, 0, 4924, 0, 10, 10, 0, 0, NULL),(48, NULL, 3, 0, 5540, 0, 10, 10, 0, 0, NULL),(49, NULL, 3, 0, 0, 405, 10, 10, 0, 0, NULL),(50, NULL, 3, 0, 615, 405, 10, 10, 0, 0, NULL),(51, NULL, 3, 0, 1231, 405, 10, 10, 0, 0, NULL),(52, NULL, 3, 0, 1846, 405, 10, 10, 0, 0, NULL),(53, NULL, 3, 0, 2462, 405, 10, 10, 0, 0, NULL),(54, NULL, 3, 0, 3077, 405, 10, 10, 0, 0, NULL),(55, NULL, 3, 0, 3693, 405, 10, 10, 0, 0, NULL),(56, NULL, 3, 0, 4308, 405, 10, 10, 0, 0, NULL),(57, NULL, 3, 0, 4924, 405, 10, 10, 0, 0, NULL),(58, NULL, 3, 0, 5540, 405, 10, 10, 0, 0, NULL),(59, NULL, 3, 0, 0, 810, 10, 10, 0, 0, NULL),(60, NULL, 3, 0, 615, 810, 10, 10, 0, 0, NULL),(61, NULL, 3, 0, 1231, 810, 10, 10, 0, 0, NULL),(62, NULL, 3, 0, 1846, 810, 10, 10, 0, 0, NULL),(63, NULL, 3, 0, 2462, 810, 10, 10, 0, 0, NULL),(64, NULL, 3, 0, 3077, 810, 10, 10, 0, 0, NULL),(65, NULL, 3, 0, 3693, 810, 10, 10, 0, 0, NULL),(66, NULL, 3, 0, 4308, 810, 10, 10, 0, 0, NULL),(67, NULL, 3, 0, 4924, 810, 10, 10, 0, 0, NULL),(68, NULL, 3, 0, 5540, 810, 10, 10, 0, 0, NULL),(69, NULL, 3, 0, 0, 1216, 10, 10, 0, 0, NULL),(70, NULL, 3, 0, 615, 1216, 10, 10, 0, 0, NULL),(71, NULL, 3, 0, 1231, 1216, 10, 10, 0, 0, NULL),(72, NULL, 3, 0, 1846, 1216, 10, 10, 0, 0, NULL),(73, NULL, 3, 0, 2462, 1216, 10, 10, 0, 0, NULL),(74, NULL, 3, 0, 3077, 1216, 10, 10, 0, 0, NULL),(75, NULL, 3, 0, 3693, 1216, 10, 10, 0, 0, NULL),(76, NULL, 3, 0, 4308, 1216, 10, 10, 0, 0, NULL),(77, NULL, 3, 0, 4924, 1216, 10, 10, 0, 0, NULL),(78, NULL, 3, 0, 5540, 1216, 10, 10, 0, 0, NULL),(79, NULL, 3, 0, 0, 1621, 10, 10, 0, 0, NULL),(80, NULL, 3, 0, 615, 1621, 10, 10, 0, 0, NULL),(81, NULL, 3, 0, 1231, 1621, 10, 10, 0, 0, NULL),(82, NULL, 3, 0, 1846, 1621, 10, 10, 0, 0, NULL),(83, NULL, 3, 0, 2462, 1621, 10, 10, 0, 0, NULL),(84, NULL, 3, 0, 3077, 1621, 10, 10, 0, 0, NULL),(85, NULL, 3, 0, 3693, 1621, 10, 10, 0, 0, NULL),(86, NULL, 3, 0, 4308, 1621, 10, 10, 0, 0, NULL),(87, NULL, 3, 0, 4924, 1621, 10, 10, 0, 0, NULL),(88, NULL, 3, 0, 5540, 1621, 10, 10, 0, 0, NULL),(89, NULL, 3, 0, 0, 2026, 10, 10, 0, 0, NULL),(90, NULL, 3, 0, 615, 2026, 10, 10, 0, 0, NULL),(91, NULL, 3, 0, 1231, 2026, 10, 10, 0, 0, NULL),(92, NULL, 3, 0, 1846, 2026, 10, 10, 0, 0, NULL),(93, NULL, 3, 0, 2462, 2026, 10, 10, 0, 0, NULL),(94, NULL, 3, 0, 3077, 2026, 10, 10, 0, 0, NULL),(95, NULL, 3, 0, 3693, 2026, 10, 10, 0, 0, NULL),(96, NULL, 3, 0, 4308, 2026, 10, 10, 0, 0, NULL),(97, NULL, 3, 0, 4924, 2026, 10, 10, 0, 0, NULL),(98, NULL, 3, 0, 5540, 2026, 10, 10, 0, 0, NULL),(99, NULL, 3, 0, 0, 2432, 10, 10, 0, 0, NULL),(100, NULL, 3, 0, 615, 2432, 10, 10, 0, 0, NULL),(101, NULL, 3, 0, 1231, 2432, 10, 10, 0, 0, NULL),(102, NULL, 3, 0, 1846, 2432, 10, 10, 0, 0, NULL),(103, NULL, 3, 0, 2462, 2432, 10, 10, 0, 0, NULL),(104, NULL, 3, 0, 3077, 2432, 10, 10, 0, 0, NULL),(105, NULL, 3, 0, 3693, 2432, 10, 10, 0, 0, NULL),(106, NULL, 3, 0, 4308, 2432, 10, 10, 0, 0, NULL),(107, NULL, 3, 0, 4924, 2432, 10, 10, 0, 0, NULL),(108, NULL, 3, 0, 5540, 2432, 10, 10, 0, 0, NULL),(109, NULL, 3, 0, 0, 2837, 10, 10, 0, 0, NULL),(110, NULL, 3, 0, 615, 2837, 10, 10, 0, 0, NULL),(111, NULL, 3, 0, 1231, 2837, 10, 10, 0, 0, NULL),(112, NULL, 3, 0, 1846, 2837, 10, 10, 0, 0, NULL),(113, NULL, 3, 0, 2462, 2837, 10, 10, 0, 0, NULL),(114, NULL, 3, 0, 3077, 2837, 10, 10, 0, 0, NULL),(115, NULL, 3, 0, 3693, 2837, 10, 10, 0, 0, NULL),(116, NULL, 3, 0, 4308, 2837, 10, 10, 0, 0, NULL),(117, NULL, 3, 0, 4924, 2837, 10, 10, 0, 0, NULL),(118, NULL, 3, 0, 5540, 2837, 10, 10, 0, 0, NULL),(119, NULL, 3, 0, 0, 3242, 10, 10, 0, 0, NULL),(120, NULL, 3, 0, 615, 3242, 10, 10, 0, 0, NULL),(121, NULL, 3, 0, 1231, 3242, 10, 10, 0, 0, NULL),(122, NULL, 3, 0, 1846, 3242, 10, 10, 0, 0, NULL),(123, NULL, 3, 0, 2462, 3242, 10, 10, 0, 0, NULL),(124, NULL, 3, 0, 3077, 3242, 10, 10, 0, 0, NULL),(125, NULL, 3, 0, 3693, 3242, 10, 10, 0, 0, NULL),(126, NULL, 3, 0, 4308, 3242, 10, 10, 0, 0, NULL),(127, NULL, 3, 0, 4924, 3242, 10, 10, 0, 0, NULL),(128, NULL, 3, 0, 5540, 3242, 10, 10, 0, 0, NULL),(129, NULL, 3, 0, 0, 3648, 10, 10, 0, 0, NULL),(130, NULL, 3, 0, 615, 3648, 10, 10, 0, 0, NULL),(131, NULL, 3, 0, 1231, 3648, 10, 10, 0, 0, NULL),(132, NULL, 3, 0, 1846, 3648, 10, 10, 0, 0, NULL),(133, NULL, 3, 0, 2462, 3648, 10, 10, 0, 0, NULL),(134, NULL, 3, 0, 3077, 3648, 10, 10, 0, 0, NULL),(135, NULL, 3, 0, 3693, 3648, 10, 10, 0, 0, NULL),(136, NULL, 3, 0, 4308, 3648, 10, 10, 0, 0, NULL),(137, NULL, 3, 0, 4924, 3648, 10, 10, 0, 0, NULL),(138, NULL, 3, 0, 5540, 3648, 10, 10, 0, 0, NULL),(139, NULL, 4, 0, 0, 0, 10, 10, 0, 0, NULL),(140, NULL, 4, 0, 4788, 0, 10, 10, 0, 0, NULL),(141, NULL, 4, 0, 9576, 0, 10, 10, 0, 0, NULL),(142, NULL, 4, 0, 0, 3194, 10, 10, 0, 0, NULL),(143, NULL, 4, 0, 4788, 3194, 10, 10, 0, 0, NULL),(144, NULL, 4, 0, 9576, 3194, 10, 10, 0, 0, NULL),(145, NULL, 4, 0, 0, 6388, 10, 10, 0, 0, NULL),(146, NULL, 4, 0, 4788, 6388, 10, 10, 0, 0, NULL),(147, NULL, 4, 0, 9576, 6388, 10, 10, 0, 0, NULL),(154, NULL, 5, 0, 4618, 1846, 10, 10, 0, 0, NULL),(155, NULL, 5, 0, 3695, 3444, 10, 10, 0, 0, NULL),(156, NULL, 5, 0, 1849, 3444, 10, 10, 0, 0, NULL),(157, NULL, 5, 0, 926, 1846, 10, 10, 0, 0, NULL),(158, NULL, 5, 0, 1848, 247, 10, 10, 0, 0, NULL),(159, NULL, 5, 0, 3695, 247, 10, 10, 0, 0, NULL),(160, NULL, 5, 0, 2463, 518, 10, 10, 0, 0, NULL),(161, NULL, 5, 0, 2176, 545, 677, 677, 0, 0, NULL),(162, NULL, 5, 0, 3198, 1107, 665, 665, 0, 0, NULL),(169, NULL, 6, 0, 0, 0, 50, 50, 0, 0, NULL),(170, NULL, 6, 0, 277, 0, 50, 50, 0, 0, NULL),(171, NULL, 6, 0, 554, 0, 50, 50, 0, 0, NULL),(172, NULL, 6, 0, 0, 184, 50, 50, 0, 0, NULL),(173, NULL, 6, 0, 277, 184, 50, 50, 0, 0, NULL),(174, NULL, 6, 0, 554, 184, 50, 50, 0, 0, NULL),(175, NULL, 6, 0, 0, 369, 50, 50, 0, 0, NULL),(176, NULL, 6, 0, 277, 369, 50, 50, 0, 0, NULL),(177, NULL, 6, 0, 554, 369, 50, 50, 0, 0, NULL);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_algorithm_poi_template_master` WRITE;
DELETE FROM `cv`.`t_scgd_algorithm_poi_template_master`;
INSERT INTO `cv`.`t_scgd_algorithm_poi_template_master` (`id`,`name`,`type`,`width`,`height`,`left_top_x`,`left_top_y`,`right_top_x`,`right_top_y`,`right_bottom_x`,`right_bottom_y`,`left_bottom_x`,`left_bottom_y`,`cfg_json`,`dynamics`,`create_date`,`is_enable`,`is_delete`,`tenant_id`,`remark`) VALUES (1, 'default1', 0, 5544, 3684, 0, 0, 5544, 0, 5544, 3684, 0, 3684, '{\"IsShowDatum\":true,\"IsShowDatumArea\":true,\"X1\":\"0,0\",\"X2\":\"5544,0\",\"X3\":\"5544,3684\",\"X4\":\"0,3684\",\"Center\":\"2772,1842\",\"PointType\":1,\"AreaCircleRadius\":1842,\"AreaCircleNum\":10,\"AreaCircleAngle\":10,\"AreaRectWidth\":5544,\"AreaRectHeight\":3684,\"AreaRectRow\":3,\"AreaRectCol\":3,\"AreaPolygonRow\":3,\"AreaPolygonCol\":3,\"Polygon1\":\"0,0\",\"Polygon2\":\"5544,0\",\"Polygon3\":\"5544,3684\",\"Polygon4\":\"0,3684\",\"DefaultCircleRadius\":1010,\"DefaultRectWidth\":20,\"DefaultRectHeight\":20,\"LedLen1\":0.0,\"LedLen2\":0.0,\"LedLen3\":0.0,\"LedLen4\":0.0}', 0, '2023-11-15 16:17:43', 1, 0, 0, NULL),(2, 'default2', 0, 6000, 4000, 0, 0, 6000, 0, 6000, 4000, 0, 4000, '{\"IsShowDatum\":true,\"IsShowDatumArea\":true,\"X1\":\"0,0\",\"X2\":\"6000,0\",\"X3\":\"6000,4000\",\"X4\":\"0,4000\",\"Center\":\"3000,2000\",\"PointType\":0,\"AreaCircleRadius\":2000,\"AreaCircleNum\":6,\"AreaCircleAngle\":0,\"AreaRectWidth\":6000,\"AreaRectHeight\":4000,\"AreaRectRow\":3,\"AreaRectCol\":3,\"AreaPolygonRow\":3,\"AreaPolygonCol\":3,\"Polygon1\":\"0,0\",\"Polygon2\":\"6000,0\",\"Polygon3\":\"6000,4000\",\"Polygon4\":\"0,4000\",\"DefaultCircleRadius\":10,\"DefaultRectWidth\":20,\"DefaultRectHeight\":20,\"LedLen1\":0.0,\"LedLen2\":0.0,\"LedLen3\":0.0,\"LedLen4\":0.0}', 0, '2023-11-14 17:56:21', 1, 0, 0, NULL),(3, 'default3', 0, 5540, 3648, 10, 10, 5530, 10, 5530, 3638, 10, 3638, '{\"IsShowDatum\":false,\"IsShowDatumArea\":false,\"X1\":\"10,10\",\"X2\":\"5530,10\",\"X3\":\"5530,3638\",\"X4\":\"10,3638\",\"Center\":\"2770,1824\",\"PointType\":0,\"AreaCircleRadius\":1824,\"AreaCircleNum\":6,\"AreaCircleAngle\":0,\"AreaRectWidth\":5540,\"AreaRectHeight\":3648,\"AreaRectRow\":10,\"AreaRectCol\":10,\"AreaPolygonRow\":3,\"AreaPolygonCol\":3,\"Polygon1\":\"0,0\",\"Polygon2\":\"5540,0\",\"Polygon3\":\"5540,3648\",\"Polygon4\":\"0,3648\",\"DefaultCircleRadius\":10,\"DefaultRectWidth\":20,\"DefaultRectHeight\":20,\"LedLen1\":0.0,\"LedLen2\":0.0,\"LedLen3\":0.0,\"LedLen4\":0.0}', 0, '2023-11-14 17:52:01', 1, 0, 0, NULL),(4, 'default4', 0, 9576, 6388, 0, 0, 9576, 0, 9576, 6388, 0, 6388, '{\"IsShowDatum\":true,\"IsShowDatumArea\":false,\"X1\":\"0,0\",\"X2\":\"9576,0\",\"X3\":\"9576,6388\",\"X4\":\"0,6388\",\"Center\":\"4788,3194\",\"PointType\":2,\"AreaCircleRadius\":3194,\"AreaCircleNum\":6,\"AreaCircleAngle\":0,\"AreaRectWidth\":9576,\"AreaRectHeight\":6388,\"AreaRectRow\":3,\"AreaRectCol\":3,\"AreaPolygonRow\":3,\"AreaPolygonCol\":3,\"Polygon1\":\"0,0\",\"Polygon2\":\"9576,0\",\"Polygon3\":\"9576,6388\",\"Polygon4\":\"0,6388\",\"DefaultCircleRadius\":10,\"DefaultRectWidth\":20,\"DefaultRectHeight\":20,\"LedLen1\":0.0,\"LedLen2\":0.0,\"LedLen3\":0.0,\"LedLen4\":0.0}', 0, '2023-10-19 15:27:25', 1, 0, 0, NULL),(5, 'p2', 0, 5544, 3692, 0, 0, 5544, 0, 5544, 3692, 0, 3692, '{\"IsShowDatum\":true,\"IsShowDatumArea\":true,\"X1\":\"0,0\",\"X2\":\"5544,0\",\"X3\":\"5544,3692\",\"X4\":\"0,3692\",\"Center\":\"2772,1846\",\"PointType\":1,\"AreaCircleRadius\":1846,\"AreaCircleNum\":6,\"AreaCircleAngle\":0,\"AreaRectWidth\":5544,\"AreaRectHeight\":3692,\"AreaRectRow\":3,\"AreaRectCol\":3,\"AreaPolygonRow\":3,\"AreaPolygonCol\":3,\"Polygon1\":\"0,0\",\"Polygon2\":\"5544,0\",\"Polygon3\":\"5544,3692\",\"Polygon4\":\"0,3692\",\"DefaultCircleRadius\":10,\"DefaultRectWidth\":20,\"DefaultRectHeight\":20,\"LedLen1\":0.0,\"LedLen2\":0.0,\"LedLen3\":0.0,\"LedLen4\":0.0}', 0, '2023-10-26 17:45:48', 1, 0, 0, NULL),(6, 'default5', 0, 5544, 3692, 0, 0, 5544, 0, 5544, 3692, 0, 3692, '{\"IsShowDatum\":true,\"IsShowDatumArea\":true,\"X1\":\"0,0\",\"X2\":\"5544,0\",\"X3\":\"5544,3692\",\"X4\":\"0,3692\",\"Center\":\"2772,1846\",\"PointType\":2,\"AreaCircleRadius\":1846,\"AreaCircleNum\":6,\"AreaCircleAngle\":0,\"AreaRectWidth\":5544,\"AreaRectHeight\":3692,\"AreaRectRow\":3,\"AreaRectCol\":3,\"AreaPolygonRow\":3,\"AreaPolygonCol\":3,\"Polygon1\":\"0,0\",\"Polygon2\":\"554,0\",\"Polygon3\":\"554,369\",\"Polygon4\":\"0,369\",\"DefaultCircleRadius\":50,\"DefaultRectWidth\":20,\"DefaultRectHeight\":20,\"LedLen1\":0.0,\"LedLen2\":0.0,\"LedLen3\":0.0,\"LedLen4\":0.0}', 0, '2023-10-27 18:06:43', 1, 0, 0, NULL);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_algorithm_result_detail_poi_mtf` WRITE;
DELETE FROM `cv`.`t_scgd_algorithm_result_detail_poi_mtf`;
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_algorithm_result_master` WRITE;
DELETE FROM `cv`.`t_scgd_algorithm_result_master`;
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_algorithm_sfr_result` WRITE;
DELETE FROM `cv`.`t_scgd_algorithm_sfr_result`;
INSERT INTO `cv`.`t_scgd_algorithm_sfr_result` (`id`,`batch_id`,`img_id`,`value`,`pdfrequency`,`pdomainSamplingData`,`ret`) VALUES (1, 221, 1, 'e23', '[1,2]', '[1,2]', 1),(2, 224, 213, '2312', '1', '1', 1);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_license` WRITE;
DELETE FROM `cv`.`t_scgd_license`;
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_measure_batch` WRITE;
DELETE FROM `cv`.`t_scgd_measure_batch`;
INSERT INTO `cv`.`t_scgd_measure_batch` (`id`,`t_id`,`name`,`code`,`create_date`,`total_time`,`result`,`tenant_id`) VALUES (1, NULL, '20231108T114946.6780337', '20231108T114946.6780337', '2023-11-08 11:49:47', 0, NULL, 0),(2, NULL, '20231108T114955.2111759', '20231108T114955.2111759', '2023-11-08 11:49:55', 0, NULL, 0),(3, NULL, '20231108T115233.5994079', '20231108T115233.5994079', '2023-11-08 11:52:34', 0, NULL, 0),(4, NULL, '20231108T115245.4393542', '20231108T115245.4393542', '2023-11-08 11:52:45', 0, NULL, 0),(5, NULL, '20231108T115427.7842262', '20231108T115427.7842262', '2023-11-08 11:54:28', 0, NULL, 0),(6, NULL, '20231108T120659.5044196', '20231108T120659.5044196', '2023-11-08 12:07:00', 0, NULL, 0),(7, NULL, '20231108T120740.6399286', '20231108T120740.6399286', '2023-11-08 12:07:41', 0, NULL, 0),(8, NULL, '20231108T120747.2413179', '20231108T120747.2413179', '2023-11-08 12:07:47', 0, NULL, 0),(9, NULL, '20231108T120809.0806128', '20231108T120809.0806128', '2023-11-08 12:08:09', 0, NULL, 0),(10, NULL, '20231108T120816.9200924', '20231108T120816.9200924', '2023-11-08 12:08:17', 0, NULL, 0),(11, NULL, '20231108T121618.7848377', '20231108T121618.7848377', '2023-11-08 12:16:19', 0, NULL, 0),(12, NULL, '20231108T121703.1976991', '20231108T121703.1976991', '2023-11-08 12:17:03', 0, NULL, 0),(13, NULL, '20231109T153131.3361868', '20231109T153131.3361868', '2023-11-09 15:31:31', 0, NULL, 0),(14, NULL, '20231109T153139.4236628', '20231109T153139.4236628', '2023-11-09 15:31:39', 0, NULL, 0),(15, NULL, '20231109T153933.6949158', '20231109T153933.6949158', '2023-11-09 15:39:34', 0, NULL, 0),(16, NULL, '20231109T154020.7110258', '20231109T154020.7110258', '2023-11-09 15:40:21', 0, NULL, 0),(17, NULL, '20231109T154405.9350997', '20231109T154405.9350997', '2023-11-09 15:44:06', 0, NULL, 0),(18, NULL, '20231109T160744.9082116', '20231109T160744.9082116', '2023-11-09 16:07:45', 0, NULL, 0),(19, NULL, '20231109T160912.0359777', '20231109T160912.0359777', '2023-11-09 16:09:12', 0, NULL, 0),(20, NULL, '20231109T160927.2679651', '20231109T160927.2679651', '2023-11-09 16:09:27', 0, NULL, 0),(21, NULL, '20231109T161013.8118819', '20231109T161013.8118819', '2023-11-09 16:10:14', 0, NULL, 0),(22, NULL, '20231109T161216.0117899', '20231109T161216.0117899', '2023-11-09 16:12:16', 0, NULL, 0),(23, NULL, '20231109T161318.7794714', '20231109T161318.7794714', '2023-11-09 16:13:19', 0, NULL, 0),(24, NULL, '20231109T161413.4195536', '20231109T161413.4195536', '2023-11-09 16:14:13', 0, NULL, 0),(25, NULL, '20231109T161542.0034023', '20231109T161542.0034023', '2023-11-09 16:15:42', 0, NULL, 0),(26, NULL, '20231109T161651.0513769', '20231109T161651.0513769', '2023-11-09 16:16:51', 0, NULL, 0),(27, NULL, '20231109T161758.3151335', '20231109T161758.3151335', '2023-11-09 16:17:58', 0, NULL, 0),(28, NULL, '20231109T163058.4179707', '20231109T163058.4179707', '2023-11-09 16:30:58', 0, NULL, 0),(29, NULL, '20231109T163115.1298948', '20231109T163115.1298948', '2023-11-09 16:31:15', 0, NULL, 0),(30, NULL, '20231109T163156.8897723', '20231109T163156.8897723', '2023-11-09 16:31:57', 0, NULL, 0),(31, NULL, '20231109T163429.6418456', '20231109T163429.6418456', '2023-11-09 16:34:30', 0, NULL, 0),(32, NULL, '20231109T163443.1695576', '20231109T163443.1695576', '2023-11-09 16:34:43', 0, NULL, 0),(33, NULL, '20231109T173726.8399815', '20231109T173726.8399815', '2023-11-09 17:37:27', 0, NULL, 0),(34, NULL, '20231109T173929.1341442', '20231109T173929.1341442', '2023-11-09 17:39:29', 0, NULL, 0),(35, NULL, '20231109T174004.9360977', '20231109T174004.9360977', '2023-11-09 17:40:05', 0, NULL, 0),(36, NULL, '20231109T174041.1578139', '20231109T174041.1578139', '2023-11-09 17:40:41', 0, NULL, 0),(37, NULL, '20231110T112719.3627754', '20231110T112719.3627754', '2023-11-10 11:27:19', 0, NULL, 0),(38, NULL, '20231110T112844.0085169', '20231110T112844.0085169', '2023-11-10 11:28:44', 0, NULL, 0),(39, NULL, '20231110T112938.4584754', '20231110T112938.4584754', '2023-11-10 11:29:38', 0, NULL, 0),(40, NULL, '20231110T112947.2433435', '20231110T112947.2433435', '2023-11-10 11:29:47', 0, NULL, 0),(41, NULL, '20231110T113022.5789402', '20231110T113022.5789402', '2023-11-10 11:30:23', 0, NULL, 0),(42, NULL, '20231110T113145.3056370', '20231110T113145.3056370', '2023-11-10 11:31:45', 0, NULL, 0),(43, NULL, '20231110T113153.9384425', '20231110T113153.9384425', '2023-11-10 11:31:54', 0, NULL, 0),(44, NULL, '20231110T113202.3305770', '20231110T113202.3305770', '2023-11-10 11:32:02', 0, NULL, 0),(45, NULL, '20231110T113222.1529252', '20231110T113222.1529252', '2023-11-10 11:32:22', 0, NULL, 0),(46, NULL, '20231110T113438.6499253', '20231110T113438.6499253', '2023-11-10 11:34:39', 0, NULL, 0),(47, NULL, '20231110T115414.1512811', '20231110T115414.1512811', '2023-11-10 11:54:14', 0, NULL, 0),(48, NULL, '20231110T115841.0150002', '20231110T115841.0150002', '2023-11-10 11:58:41', 0, NULL, 0),(49, NULL, '20231110T120052.2378638', '20231110T120052.2378638', '2023-11-10 12:00:52', 0, NULL, 0),(50, NULL, '20231113T021031.7509562', '20231113T021031.7509562', '2023-11-13 02:10:32', 0, NULL, 0),(51, NULL, '20231113T021052.3616048', '20231113T021052.3616048', '2023-11-13 02:10:52', 0, NULL, 0),(52, NULL, '20231113T021130.8969598', '20231113T021130.8969598', '2023-11-13 02:11:31', 0, NULL, 0),(53, NULL, '20231113T021259.2091072', '20231113T021259.2091072', '2023-11-13 02:12:59', 0, NULL, 0),(54, NULL, '20231113T021529.1610826', '20231113T021529.1610826', '2023-11-13 02:15:29', 0, NULL, 0),(55, NULL, '20231113T155627.7271664', '20231113T155627.7271664', '2023-11-13 15:56:28', 0, NULL, 0),(56, NULL, '20231113T162549.3755243', '20231113T162549.3755243', '2023-11-13 16:25:49', 0, NULL, 0),(57, NULL, '20231113T171514.5862177', '20231113T171514.5862177', '2023-11-13 17:15:15', 0, NULL, 0),(58, NULL, '20231113T171525.7292581', '20231113T171525.7292581', '2023-11-13 17:15:26', 0, NULL, 0),(59, NULL, '20231114T103737.6027842', '20231114T103737.6027842', '2023-11-14 10:37:38', 0, NULL, 0),(60, NULL, '20231114T111243.2207468', '20231114T111243.2207468', '2023-11-14 11:12:43', 0, NULL, 0),(61, NULL, '20231114T111626.8449229', '20231114T111626.8449229', '2023-11-14 11:16:27', 0, NULL, 0),(62, NULL, '20231114T111637.0083836', '20231114T111637.0083836', '2023-11-14 11:16:37', 0, NULL, 0),(63, NULL, '20231114T111805.0613387', '20231114T111805.0613387', '2023-11-14 11:18:05', 0, NULL, 0),(64, NULL, '20231114T111811.6403732', '20231114T111811.6403732', '2023-11-14 11:18:12', 0, NULL, 0),(65, NULL, '20231114T121353.1095326', '20231114T121353.1095326', '2023-11-14 12:13:53', 0, NULL, 0),(66, NULL, '20231115T115738.6208570', '20231115T115738.6208570', '2023-11-15 11:57:39', 0, NULL, 0),(67, NULL, '20231115T115801.1828057', '20231115T115801.1828057', '2023-11-15 11:58:01', 0, NULL, 0),(68, NULL, '20231115T120340.0932638', '20231115T120340.0932638', '2023-11-15 12:03:40', 0, NULL, 0),(69, NULL, '20231115T120347.8745041', '20231115T120347.8745041', '2023-11-15 12:03:48', 0, NULL, 0),(70, NULL, '20231115T120355.1124179', '20231115T120355.1124179', '2023-11-15 12:03:55', 0, NULL, 0),(71, NULL, '20231115T141624.6910558', '20231115T141624.6910558', '2023-11-15 14:16:25', 0, NULL, 0),(72, NULL, '20231123T134302.0380663', '20231123T134302.0380663', '2023-11-23 13:43:02', 0, NULL, 0),(73, NULL, '20231123T134331.4923323', '20231123T134331.4923323', '2023-11-23 13:43:31', 0, NULL, 0),(74, NULL, '20231123T134335.5881059', '20231123T134335.5881059', '2023-11-23 13:43:36', 0, NULL, 0),(75, NULL, '20231123T134341.4179031', '20231123T134341.4179031', '2023-11-23 13:43:41', 0, NULL, 0),(76, NULL, '20231123T134342.5864273', '20231123T134342.5864273', '2023-11-23 13:43:43', 0, NULL, 0),(77, NULL, '20231123T134417.1680011', '20231123T134417.1680011', '2023-11-23 13:44:17', 0, NULL, 0),(78, NULL, '20231123T134527.7059103', '20231123T134527.7059103', '2023-11-23 13:45:28', 0, NULL, 0),(79, NULL, '20231123T135617.8040918', '20231123T135617.8040918', '2023-11-23 13:56:18', 0, NULL, 0),(80, NULL, '20231123T135620.4573148', '20231123T135620.4573148', '2023-11-23 13:56:20', 0, NULL, 0),(81, NULL, '20231123T135649.1454540', '20231123T135649.1454540', '2023-11-23 13:56:49', 0, NULL, 0),(82, NULL, '20231123T143458.4162885', '20231123T143458.4162885', '2023-11-23 14:34:58', 0, NULL, 0),(83, NULL, '20231123T144326.2404919', '20231123T144326.2404919', '2023-11-23 14:43:26', 0, NULL, 0),(84, NULL, '20231123T145627.3570003', '20231123T145627.3570003', '2023-11-23 14:56:27', 0, NULL, 0),(85, NULL, '20231123T145720.8081041', '20231123T145720.8081041', '2023-11-23 14:57:21', 0, NULL, 0),(86, NULL, '20231123T145907.0551271', '20231123T145907.0551271', '2023-11-23 14:59:07', 0, NULL, 0),(87, NULL, '20231123T151926.5434362', '20231123T151926.5434362', '2023-11-23 15:19:27', 0, NULL, 0),(88, NULL, '20231123T151958.3426636', '20231123T151958.3426636', '2023-11-23 15:19:58', 0, NULL, 0);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_measure_result_detail` WRITE;
DELETE FROM `cv`.`t_scgd_measure_result_detail`;
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_measure_result_img` WRITE;
DELETE FROM `cv`.`t_scgd_measure_result_img`;
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_measure_result_smu` WRITE;
DELETE FROM `cv`.`t_scgd_measure_result_smu`;
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_measure_result_smu_scan` WRITE;
DELETE FROM `cv`.`t_scgd_measure_result_smu_scan`;
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_measure_result_spectrometer` WRITE;
DELETE FROM `cv`.`t_scgd_measure_result_spectrometer`;
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_measure_template_detail` WRITE;
DELETE FROM `cv`.`t_scgd_measure_template_detail`;
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_measure_template_master` WRITE;
DELETE FROM `cv`.`t_scgd_measure_template_master`;
INSERT INTO `cv`.`t_scgd_measure_template_master` (`id`,`name`,`create_date`,`is_enable`,`is_delete`,`remark`,`tenant_id`) VALUES (1, 'default1', '2023-10-07 17:37:27', 1, 1, NULL, 0),(2, 'default1', '2023-10-07 17:37:45', 1, 0, NULL, 0);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_mod_param_detail` WRITE;
DELETE FROM `cv`.`t_scgd_mod_param_detail`;
INSERT INTO `cv`.`t_scgd_mod_param_detail` (`id`,`cc_pid`,`value_a`,`value_b`,`pid`,`is_enable`,`is_delete`) VALUES (1, 206, '123', '12', 1, 1, 0),(2, 207, '123', '12', 1, 1, 0),(3, 208, '12312312', '1231231', 1, 1, 0),(4, 209, '123123', '12312', 1, 1, 0),(5, 210, '213', '21', 1, 1, 0),(6, 211, NULL, NULL, 1, 1, 0),(7, 212, '222', NULL, 1, 1, 0),(8, 213, '213', '21', 1, 1, 0),(9, 214, NULL, NULL, 1, 1, 0),(10, 215, NULL, NULL, 1, 1, 0),(11, 216, '213', '21', 1, 1, 0),(12, 217, '233333', '23333', 1, 1, 0),(13, 218, NULL, NULL, 1, 1, 0),(14, 219, '222', NULL, 1, 1, 0),(15, 220, '123', '12', 1, 1, 0),(16, 221, '222', NULL, 1, 1, 0),(17, 222, '3333', '333', 1, 1, 0),(18, 223, '213', '21', 1, 1, 0),(19, 224, '213', '21', 1, 1, 0),(20, 225, '请问请问', '请问请问', 1, 1, 0),(21, 226, '123', '12', 1, 1, 0),(22, 227, '213', '21', 1, 1, 0),(32, 206, '213213', '21321', 2, 1, 0),(33, 207, NULL, NULL, 2, 1, 0),(34, 208, '213123', '21312', 2, 1, 0),(35, 209, NULL, NULL, 2, 1, 0),(36, 210, NULL, NULL, 2, 1, 0),(37, 211, NULL, NULL, 2, 1, 0),(38, 212, NULL, NULL, 2, 1, 0),(39, 213, '213123', '21312', 2, 1, 0),(40, 214, NULL, NULL, 2, 1, 0),(41, 215, NULL, NULL, 2, 1, 0),(42, 216, '请问e', '请问e', 2, 1, 0),(43, 217, NULL, NULL, 2, 1, 0),(44, 218, NULL, NULL, 2, 1, 0),(45, 219, '123123', '12312', 2, 1, 0),(46, 220, NULL, NULL, 2, 1, 0),(47, 221, NULL, NULL, 2, 1, 0),(48, 222, '123213', '12321', 2, 1, 0),(49, 223, NULL, NULL, 2, 1, 0),(50, 224, NULL, NULL, 2, 1, 0),(51, 225, '213123123', '21312312', 2, 1, 0),(52, 226, NULL, NULL, 2, 1, 0),(53, 227, NULL, NULL, 2, 1, 0),(63, 206, NULL, NULL, 3, 1, 0),(64, 207, NULL, NULL, 3, 1, 0),(65, 208, NULL, NULL, 3, 1, 0),(66, 209, NULL, NULL, 3, 1, 0),(67, 210, NULL, NULL, 3, 1, 0),(68, 211, NULL, NULL, 3, 1, 0),(69, 212, NULL, NULL, 3, 1, 0),(70, 213, NULL, NULL, 3, 1, 0),(71, 214, NULL, NULL, 3, 1, 0),(72, 215, NULL, NULL, 3, 1, 0),(73, 216, NULL, NULL, 3, 1, 0),(74, 217, NULL, NULL, 3, 1, 0),(75, 218, NULL, NULL, 3, 1, 0),(76, 219, NULL, NULL, 3, 1, 0),(77, 220, NULL, NULL, 3, 1, 0),(78, 221, NULL, NULL, 3, 1, 0),(79, 222, NULL, NULL, 3, 1, 0),(80, 223, NULL, NULL, 3, 1, 0),(81, 224, NULL, NULL, 3, 1, 0),(82, 225, NULL, NULL, 3, 1, 0),(83, 226, NULL, NULL, 3, 1, 0),(84, 227, NULL, NULL, 3, 1, 0),(94, 206, NULL, NULL, 4, 1, 0),(95, 207, NULL, NULL, 4, 1, 0),(96, 208, NULL, NULL, 4, 1, 0),(97, 209, NULL, NULL, 4, 1, 0),(98, 210, NULL, NULL, 4, 1, 0),(99, 211, NULL, NULL, 4, 1, 0),(100, 212, NULL, NULL, 4, 1, 0),(101, 213, NULL, NULL, 4, 1, 0),(102, 214, NULL, NULL, 4, 1, 0),(103, 215, NULL, NULL, 4, 1, 0),(104, 216, NULL, NULL, 4, 1, 0),(105, 217, NULL, NULL, 4, 1, 0),(106, 218, NULL, NULL, 4, 1, 0),(107, 219, NULL, NULL, 4, 1, 0),(108, 220, NULL, NULL, 4, 1, 0),(109, 221, NULL, NULL, 4, 1, 0),(110, 222, NULL, NULL, 4, 1, 0),(111, 223, NULL, NULL, 4, 1, 0),(112, 224, NULL, NULL, 4, 1, 0),(113, 225, NULL, NULL, 4, 1, 0),(114, 226, NULL, NULL, 4, 1, 0),(115, 227, NULL, NULL, 4, 1, 0);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_mod_param_master` WRITE;
DELETE FROM `cv`.`t_scgd_mod_param_master`;
INSERT INTO `cv`.`t_scgd_mod_param_master` (`id`,`name`,`mm_id`,`mod_no`,`create_date`,`is_enable`,`is_delete`,`remark`,`tenant_id`) VALUES (1, 'default1', 2, NULL, '2023-12-01 16:10:25', 1, 0, NULL, 0),(2, 'default2', 2, NULL, '2023-12-01 16:19:41', 1, 0, NULL, 0),(3, 'default3', 2, NULL, '2023-12-01 16:19:42', 1, 0, NULL, 0),(4, 'default4', 2, NULL, '2023-12-01 16:19:42', 1, 0, NULL, 0);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_rc_app` WRITE;
DELETE FROM `cv`.`t_scgd_rc_app`;
INSERT INTO `cv`.`t_scgd_rc_app` (`id`,`name`,`app_id`,`app_secret`,`tenant_id`,`remark`,`is_enable`) VALUES (1, 'app1', 'app1', '123456', 0, NULL, 1),(2, 'app2', 'app2', '654321', 0, NULL, 1);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_rc_app_nodes` WRITE;
DELETE FROM `cv`.`t_scgd_rc_app_nodes`;
INSERT INTO `cv`.`t_scgd_rc_app_nodes` (`id`,`pid`,`node_type`,`node_name`,`node_services`,`is_enable`) VALUES (1, 1, 'pg', NULL, NULL, 1),(2, 1, 'Spectum', NULL, NULL, 1),(3, 1, 'SMU', NULL, NULL, 1),(4, 1, 'client', NULL, NULL, 1),(5, 2, 'client', NULL, NULL, 1),(6, 2, 'Spectum', NULL, NULL, 1),(7, 1, 'FileServer', NULL, NULL, 1),(13, 1, 'Algorithm', NULL, NULL, 1);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_rc_sys_node` WRITE;
DELETE FROM `cv`.`t_scgd_rc_sys_node`;
INSERT INTO `cv`.`t_scgd_rc_sys_node` (`id`,`name`,`rc_name`,`type_codes`,`app_id`,`app_key`,`tenant_id`,`is_enable`,`is_delete`,`mqtt_cfg_id`,`create_date`,`remark`) VALUES (1, 'local_x64', 'RC_local', '[FileServer,pg,Algorithm,SMU]', 'app1', '123456', 0, 1, 0, 1, '2023-11-10 10:09:56', NULL),(2, 'local_x86', 'RC_local', '[Spectum]', 'app1', '123456', 0, 1, 0, 1, '2023-11-10 10:10:31', NULL);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_sys_camera` WRITE;
DELETE FROM `cv`.`t_scgd_sys_camera`;
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_sys_dictionary` WRITE;
DELETE FROM `cv`.`t_scgd_sys_dictionary`;
INSERT INTO `cv`.`t_scgd_sys_dictionary` (`id`,`name`,`pid`,`key`,`val`,`is_enable`,`is_delete`,`remark`,`tenant_id`) VALUES (1, '服务类别', NULL, 'service_type', NULL, 1, 0, NULL, 0),(2, '相机', 1, 'camera', 1, 1, 0, NULL, 0),(3, 'PG', 1, 'pg', 2, 1, 0, 'PGWindowsService.PGDevice, SensorWindowsService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null', 0),(4, '光谱仪', 1, 'Spectum', 3, 1, 0, 'SpectumWindowsService.GCSDevice, SpectumWindowsService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null', 0),(5, '源表', 1, 'SMU', 4, 1, 0, 'SMUWindowsService.SMUDevice, SMUWindowsService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null', 0),(6, '通用传感器', 1, 'sensor', 5, 1, 0, NULL, 0),(7, '文件服务', 1, 'FileServer', 6, 1, 0, 'FileServerWindowsService.FileServerDevice, FileServerWindowsService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null', 0),(10, '流程类别', NULL, 'flow_type', NULL, 1, 0, NULL, 0),(11, '流程模板', 10, 'flow_temp', 101, 1, 0, NULL, 0),(12, '算法服务', 1, 'Algorithm', 7, 1, 0, 'AlgorithmPlugin.POI.POIDevice, AlgorithmPlugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null', 0),(13, '滤色轮服务', 1, 'CfwPort', 8, 1, 0, NULL, 0),(14, '校正服务', 1, 'Calibration', 9, 1, 0, NULL, 0),(15, '电机服务', 1, 'Motor', 10, 1, 0, NULL, 0),(16, '对焦环', 1, 'FocusRing', 11, 1, 0, NULL, 0),(30, '校正', NULL, 'Calibration', NULL, 1, 0, NULL, 0),(31, '均匀场', 30, 'uniformity', 20, 1, 0, NULL, 0),(32, '畸变', 30, 'distortion', 21, 1, 0, NULL, 0),(33, '色偏', 30, 'color_shift', 22, 1, 0, NULL, 0),(34, '亮度', 30, 'luminance', 23, 1, 0, NULL, 0),(35, '单色', 30, 'colorone', 24, 1, 0, NULL, 0),(36, '四色', 30, 'colorfour', 25, 1, 0, NULL, 0),(37, '多色', 30, 'colormulti', 26, 1, 0, NULL, 0);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_sys_dictionary_mod_item` WRITE;
DELETE FROM `cv`.`t_scgd_sys_dictionary_mod_item`;
INSERT INTO `cv`.`t_scgd_sys_dictionary_mod_item` (`id`,`symbol`,`address_code`,`name`,`val_type`,`value_range`,`default_val`,`pid`,`create_date`,`is_enable`,`is_delete`,`remark`) VALUES (101, 'ob', 101, 'OB区左', 0, NULL, '4', 1, '2023-06-27 17:36:20', 1, 0, NULL),(102, 'obR', 102, 'OB区右', 0, NULL, '0', 1, '2023-06-27 17:36:22', 1, 0, NULL),(103, 'obT', 103, 'OB区上', 0, NULL, '0', 1, '2023-06-27 17:36:23', 1, 0, NULL),(104, 'obB', 104, 'OB区下', 0, NULL, '0', 1, '2023-06-27 17:36:25', 1, 0, NULL),(105, 'tempCtlChecked', 105, '温度检测', 2, NULL, 'True', 1, '2023-06-27 17:36:31', 1, 0, NULL),(106, 'targetTemp', 106, '目标温度', 0, NULL, '5', 1, '2023-06-27 17:36:35', 1, 0, NULL),(107, 'usbTraffic', 107, 'USB通讯', 0, NULL, '0', 1, '2023-06-27 17:36:38', 1, 0, NULL),(108, 'offset', 108, '移位', 0, NULL, '0', 1, '2023-06-27 17:36:38', 1, 0, NULL),(109, 'gain', 109, '增益', 0, NULL, '0', 1, '2023-06-27 17:36:42', 1, 0, NULL),(110, 'ex', 110, 'x', 0, NULL, '0', 1, '2023-06-27 17:36:43', 1, 0, NULL),(111, 'ey', 111, 'y', 0, NULL, '0', 1, '2023-06-27 17:36:43', 1, 0, NULL),(112, 'ew', 112, 'width', 0, NULL, '0', 1, '2023-06-27 17:36:44', 1, 0, NULL),(113, 'eh', 113, 'height', 0, NULL, '0', 1, '2023-06-27 17:36:45', 1, 0, NULL),(206, 'Luminance', 206, '亮度', 3, NULL, NULL, 2, '2023-12-01 15:39:38', 1, 0, NULL),(207, 'LumOneColor', 207, '单色', 3, NULL, NULL, 2, '2023-12-01 15:39:42', 1, 0, NULL),(208, 'LumFourColor', 208, '四色', 3, NULL, NULL, 2, '2023-12-01 15:39:45', 1, 0, NULL),(209, 'LumMultiColor', 209, '多色', 3, NULL, NULL, 2, '2023-12-01 15:39:47', 1, 0, NULL),(210, 'UniformityR', 210, '均匀场X(红)', 3, NULL, NULL, 2, '2023-12-01 16:19:07', 1, 0, NULL),(211, 'UniformityG', 211, '均匀场Y(绿)', 3, NULL, NULL, 2, '2023-12-01 16:19:11', 1, 0, NULL),(212, 'UniformityB', 212, '均匀场Z(蓝)', 3, NULL, NULL, 2, '2023-12-01 16:19:14', 1, 0, NULL),(213, 'DistortionR', 213, '畸变X(红)', 3, NULL, NULL, 2, '2023-12-01 16:18:58', 1, 0, NULL),(214, 'DistortionG', 214, '畸变Y(绿)', 3, NULL, NULL, 2, '2023-12-01 16:19:01', 1, 0, NULL),(215, 'DistortionB', 215, '畸变Z(蓝)', 3, NULL, NULL, 2, '2023-12-01 16:19:04', 1, 0, NULL),(216, 'ColorShiftR', 216, '色偏X(红)', 3, NULL, NULL, 2, '2023-12-01 16:18:40', 1, 0, NULL),(217, 'ColorShiftG', 217, '色偏Y(绿)', 3, NULL, NULL, 2, '2023-12-01 16:18:50', 1, 0, NULL),(218, 'ColorShiftB', 218, '色偏Z(蓝)', 3, NULL, NULL, 2, '2023-12-01 16:18:54', 1, 0, NULL),(219, 'DarkNoiseR', 219, 'DarkNoiseR', 3, NULL, NULL, 2, '2023-12-01 15:40:53', 1, 0, NULL),(220, 'DarkNoiseG', 220, 'DarkNoiseG', 3, NULL, NULL, 2, '2023-12-01 15:40:53', 1, 0, NULL),(221, 'DarkNoiseB', 221, 'DarkNoiseB', 3, NULL, NULL, 2, '2023-12-01 15:40:53', 1, 0, NULL),(222, 'DefectPointR', 222, 'DefectPointR', 3, NULL, NULL, 2, '2023-12-01 15:40:53', 1, 0, NULL),(223, 'DefectPointG', 223, 'DefectPointG', 3, NULL, NULL, 2, '2023-12-01 15:40:53', 1, 0, NULL),(224, 'DefectPointB', 224, 'DefectPointB', 3, NULL, NULL, 2, '2023-12-01 15:40:53', 1, 0, NULL),(225, 'DSNUR', 225, 'DSNUR', 3, NULL, NULL, 2, '2023-12-01 15:40:53', 1, 0, NULL),(226, 'DSNUG', 225, 'DSNUG', 3, NULL, NULL, 2, '2023-12-01 15:40:53', 1, 0, NULL),(227, 'DSNUB', 227, 'DSNUB', 3, NULL, NULL, 2, '2023-12-01 15:40:53', 1, 0, NULL),(301, 'CM_StartPG', 301, '开始', 3, NULL, 'start\\r', 3, '2023-08-28 15:37:50', 1, 0, NULL),(302, 'CM_StopPG', 302, '停止', 3, NULL, 'stop\\r', 3, '2023-08-28 15:39:06', 1, 0, NULL),(303, 'CM_ReSetPG', 303, '重置', 3, NULL, 'reset\\r', 3, '2023-08-28 15:39:14', 1, 0, NULL),(304, 'CM_SwitchUpPG', 304, '上', 3, NULL, 'key UP\\r', 3, '2023-08-28 15:39:25', 1, 0, NULL),(305, 'CM_SwitchDownPG', 305, '下', 3, NULL, 'key DN\\r', 3, '2023-08-28 15:40:08', 1, 0, NULL),(306, 'CM_SwitchFramePG', 306, '切指定', 3, NULL, 'pat {0}\\r', 3, '2023-08-28 15:41:17', 1, 0, NULL),(401, 'autoExpTimeBegin', 401, '开始时间', 0, NULL, '10', 4, '2023-06-27 17:33:45', 1, 0, NULL),(402, 'autoExpFlag', 402, '是否启用', 2, NULL, 'True', 4, '2023-06-27 17:33:51', 1, 0, NULL),(403, 'autoExpSyncFreq', 403, '频率同步', 0, NULL, '-1', 4, '2023-06-27 17:34:30', 1, 0, NULL),(404, 'autoExpSaturation', 404, '饱和度', 0, NULL, '70', 4, '2023-06-27 17:34:39', 1, 0, NULL),(405, 'autoExpSatMaxAD', 405, 'AD最大值', 0, NULL, '65000', 4, '2023-06-27 17:34:45', 1, 0, NULL),(406, 'autoExpMaxPecentage', 406, '最大值百分比', 1, NULL, '0.01', 4, '2023-06-27 17:35:18', 1, 0, NULL),(407, 'autoExpSatDev', 407, '饱和度差值', 0, NULL, '20', 4, '2023-06-27 17:35:38', 1, 0, NULL),(408, 'maxExpTime', 408, '最大曝光时间', 1, NULL, '60000', 4, '2023-06-27 17:35:45', 1, 0, NULL),(409, 'minExpTime', 409, '最小曝光时间', 1, NULL, '0.2', 4, '2023-06-27 17:35:54', 1, 0, NULL),(410, 'burstThreshold', 410, 'burst阈值', 0, NULL, '200', 4, '2023-06-27 17:35:58', 1, 0, NULL),(1101, 'filename', 1101, '文件名', 3, NULL, NULL, 11, '2023-07-05 16:41:05', 1, 0, NULL),(1201, 'Left', 1201, '左', 0, NULL, '5', 12, '2023-07-06 17:21:18', 1, 0, NULL),(1202, 'Right', 1202, '右', 0, NULL, '5', 12, '2023-07-06 17:21:19', 1, 0, NULL),(1203, 'Top', 1203, '上', 0, NULL, '5', 12, '2023-07-06 17:21:20', 1, 0, NULL),(1204, 'Bottom', 1204, '下', 0, NULL, '5', 12, '2023-07-06 17:21:21', 1, 0, NULL),(1205, 'BlurSize', 1205, 'BlurSize', 0, NULL, '19', 12, '2023-07-06 17:53:30', 1, 0, NULL),(1206, 'DilateSize', 1206, 'DilateSize', 0, NULL, '5', 12, '2023-07-06 18:09:19', 1, 0, NULL),(1207, 'FilterByContrast', 1207, 'FilterByContrast', 2, NULL, 'True', 12, '2023-07-06 18:16:56', 1, 0, NULL),(1208, 'MaxContrast', 1208, 'MaxContrast', 1, NULL, '1.7', 12, '2023-07-06 18:20:12', 1, 0, NULL),(1209, 'MinContrast', 1209, 'MinContrast', 1, NULL, '0.3', 12, '2023-07-06 18:20:32', 1, 0, NULL),(1301, 'IsSourceV', 1301, '是否电压', 2, NULL, 'True', 13, '2023-08-18 09:42:23', 1, 0, NULL),(1302, 'BeginValue', 1302, '开始值', 1, NULL, '0.0', 13, '2023-08-18 09:43:53', 1, 0, NULL),(1303, 'EndValue', 1303, '结束值', 1, NULL, '5.0', 13, '2023-08-18 09:44:38', 1, 0, NULL),(1304, 'LimitValue', 1304, '限值', 1, NULL, '200.0', 13, '2023-08-18 09:45:26', 1, 0, NULL),(1305, 'Points', 1305, '点数', 0, NULL, '100', 13, '2023-08-18 09:46:37', 1, 0, NULL),(3000, 'Gamma', 3000, 'Gamma', 1, NULL, '1.0', 9, '2023-11-07 15:24:35', 1, 0, NULL),(3001, 'X', 3001, 'X', 0, NULL, '0', 9, '2023-11-07 11:55:18', 1, 0, NULL),(3002, 'Y', 3002, 'Y', 0, NULL, '0', 9, '2023-11-07 11:55:20', 1, 0, NULL),(3003, 'Width', 3003, 'Width', 0, NULL, '1000', 9, '2023-10-10 11:42:52', 1, 0, NULL),(3004, 'Height', 3004, 'Height', 0, NULL, '1000', 9, '2023-10-10 11:43:19', 1, 0, NULL),(3010, 'Radio', 3010, '计算FOV时中心区亮度的百分比多少认为是暗区', 1, NULL, '0.2', 6, '2023-10-09 11:26:29', 1, 0, NULL),(3011, 'CameraDegrees', 3011, '相机镜头有效像素对应的角度', 1, NULL, '0.2', 6, '2023-10-09 11:26:30', 1, 0, NULL),(3012, 'ThresholdValus', 3012, 'FOV中计算圆心或者矩心时使用的二值化阈值', 0, NULL, '20', 6, '2023-10-09 11:26:30', 1, 0, NULL),(3013, 'DFovDist', 3013, '相机镜头使用的有效像素', 1, NULL, '8443', 6, '2023-10-09 11:26:31', 1, 0, NULL),(3014, 'FovPattern', 3014, '计算pattern(FovCircle-圆形；FovRectangle-矩形)', 4, NULL, '0', 6, '2023-10-09 11:26:32', 1, 0, NULL),(3015, 'FovType', 3015, '计算路线(Horizontal-水平；Vertical-垂直；Leaning-斜向)', 4, NULL, '0', 6, '2023-10-09 11:26:32', 1, 0, NULL),(3016, 'Xc', 3016, 'Xc', 1, NULL, '0', 6, '2023-11-07 10:59:49', 1, 0, NULL),(3017, 'Yc', 3017, 'Yc', 1, NULL, '0', 6, '2023-11-07 10:59:47', 1, 0, NULL),(3018, 'Xp', 3018, 'Xp', 1, NULL, '0', 6, '2023-11-07 10:59:47', 1, 0, NULL),(3019, 'Yp', 3019, 'Yp', 1, NULL, '0', 6, '2023-11-07 10:59:48', 1, 0, NULL),(3020, 'Ghost_radius', 3020, '待检测鬼影点阵的半径长度(像素)', 0, NULL, '65', 7, '2023-10-09 15:13:27', 1, 0, NULL),(3021, 'Ghost_cols', 3021, '待检测鬼影点阵的列数', 0, NULL, '3', 7, '2023-10-09 15:13:44', 1, 0, NULL),(3022, 'Ghost_rows', 3022, '待检测鬼影点阵的行数', 0, NULL, '3', 7, '2023-10-09 15:13:43', 1, 0, NULL),(3023, 'Ghost_ratioH', 3023, '待检测鬼影的中心灰度百分比上限', 1, NULL, '0.4', 7, '2023-10-09 15:13:49', 1, 0, NULL),(3024, 'Ghost_ratioL', 3024, '待检测鬼影的中心灰度百分比下限', 1, NULL, '0.2', 7, '2023-10-09 15:42:14', 1, 0, NULL),(3030, 'filterByColor', 3030, '是否使用颜色过滤', 2, NULL, 'true', 10, '2023-10-09 15:43:17', 1, 0, NULL),(3031, 'blobColor', 3031, '亮斑255暗斑0', 0, '', '0', 10, '2023-10-09 15:43:49', 1, 0, NULL),(3032, 'minThreshold', 3032, '阈值每次间隔值', 1, NULL, '10', 10, '2023-10-09 15:50:20', 1, 0, NULL),(3033, 'thresholdStep', 3033, '斑点最小灰度', 1, NULL, '10', 10, '2023-10-09 15:50:40', 1, 0, NULL),(3034, 'maxThreshold', 3034, '斑点最大灰度', 1, NULL, '220', 10, '2023-10-09 15:51:20', 1, 0, NULL),(3035, 'ifDEBUG', 3035, NULL, 2, NULL, NULL, 10, '2023-10-09 15:51:45', 1, 0, NULL),(3036, 'darkRatio', 3036, '暗斑比例', 1, NULL, '0.01', 10, '2023-10-09 15:52:11', 1, 0, NULL),(3037, 'contrastRatio', 3037, '对比度比例', 1, NULL, '0.1', 10, '2023-10-09 15:52:34', 1, 0, NULL),(3038, 'bgRadius', 3038, '背景半径', 0, NULL, '31', 10, '2023-10-09 15:53:03', 1, 0, NULL),(3039, 'minDistBetweenBlobs', 3039, '斑点间隔距离', 1, NULL, '50', 10, '2023-10-09 15:53:29', 1, 0, NULL),(3040, 'filterByArea', 3040, '是否使用面积过滤', 0, NULL, 'true', 10, '2023-10-09 15:53:59', 1, 0, NULL),(3041, 'minArea', 3041, '斑点最小面积值', 1, NULL, '200', 10, '2023-10-09 15:54:49', 1, 0, NULL),(3042, 'maxArea', 3042, '斑点最大面积值', 1, NULL, '10000', 10, '2023-10-09 15:55:13', 1, 0, NULL),(3043, 'minRepeatability', 3043, '重复次数认定', 0, NULL, '2', 10, '2023-10-09 15:55:39', 1, 0, NULL),(3044, 'filterByCircularity', 3044, '形状控制（圆，方)', 0, NULL, NULL, 10, '2023-10-09 15:57:27', 1, 0, NULL),(3045, 'minCircularity', 3045, '', 1, NULL, '0.9', 10, '2023-10-09 15:59:29', 1, 0, NULL),(3046, 'maxCircularity', 3046, NULL, 1, NULL, '3.40282346638528859e+38', 10, '2023-10-09 16:04:59', 1, 0, NULL),(3047, 'filterByConvexity', 3047, '形状控制（豁口）', 0, NULL, NULL, 10, '2023-10-09 15:59:23', 1, 0, NULL),(3048, 'minConvexity ', 3048, NULL, 1, NULL, '0.9', 10, '2023-11-22 18:06:52', 1, 0, NULL),(3049, 'maxConvexity', 3049, NULL, 1, NULL, '3.40282346638528859e+38', 10, '2023-10-09 16:04:29', 1, 0, NULL),(3050, 'filterByInertia', 3050, '形状控制（椭圆度）', 0, NULL, NULL, 10, '2023-10-09 16:00:51', 1, 0, NULL),(3051, 'minInertiaRatio', 3051, NULL, 1, NULL, '0.9', 10, '2023-10-09 16:00:51', 1, 0, NULL),(3052, 'maxInertiaRatio', 3052, NULL, 1, NULL, '3.40282346638528859e+38', 10, '2023-10-09 16:04:28', 1, 0, NULL),(3053, 'X', 3053, 'X', 0, NULL, '0', 10, '2023-10-24 15:04:15', 1, 0, NULL),(3054, 'Y', 3054, 'Y', 0, NULL, '0', 10, '2023-10-24 15:04:15', 1, 0, NULL),(3055, 'Width', 3055, 'cx', 0, NULL, '16', 10, '2023-10-24 15:31:14', 1, 0, NULL),(3056, 'Height', 3056, 'cy', 0, NULL, '11', 10, '2023-10-24 15:31:15', 1, 0, NULL),(3060, 'MTF_dRatio', 3000, 'MTF_dRatio', 1, NULL, '0.01', 8, '2023-10-08 11:59:12', 1, 0, NULL),(3061, 'EvaFunc', 3061, 'EvaFunc', 4, NULL, 'CalResol', 8, '2023-10-10 10:48:14', 1, 0, NULL),(3062, 'dx', 3062, 'dx', 0, NULL, '0', 8, '2023-10-10 10:48:39', 1, 0, NULL),(3063, 'dy', 3063, 'dy', 0, NULL, '1', 8, '2023-10-10 10:49:13', 1, 0, NULL),(3064, 'ksize', 3064, 'ksize', 0, NULL, '5', 8, '2023-10-10 10:49:14', 1, 0, NULL),(3070, 'type', 3070, 'type', 0, NULL, '0', 10, '2023-11-07 14:11:10', 1, 0, NULL),(3071, 'sType', 3071, 'sType', 0, NULL, '0', 10, '2023-11-07 14:11:10', 1, 0, NULL),(3072, 'lType', 3072, 'lType', 0, NULL, '0', 10, '2023-11-07 14:11:11', 1, 0, NULL),(3073, 'dType', 3073, 'dType', 0, NULL, '0', 10, '2023-11-07 14:11:11', 1, 0, NULL),(3080, 'CheckChannel', 3080, '灯珠抓取通道', 0, NULL, '1', 14, '2023-11-14 17:39:03', 1, 0, NULL),(3081, 'Isguding', 3081, '是否启用固定半径计算', 0, NULL, '2', 14, '2023-11-14 17:39:02', 1, 0, NULL),(3082, 'Gudingrid', 3082, '灯珠固定半径', 0, NULL, '15', 14, '2023-11-14 17:43:47', 1, 0, NULL),(3083, 'Lunkuomianji', 3083, '轮廓最小面积', 0, NULL, '5', 14, '2023-11-14 17:43:57', 1, 0, NULL),(3084, 'PointNum', 3084, 'PointNum', 0, NULL, '0', 14, '2023-11-14 16:14:41', 1, 0, NULL),(3085, 'Hegexishu', 3085, '轮廓范围系数', 1, NULL, '0.3', 14, '2023-11-14 17:44:15', 1, 0, NULL),(3086, 'Erzhihuapiancha', 3086, '图像二值化补正', 0, NULL, '-20', 14, '2023-11-14 17:44:26', 1, 0, NULL),(3087, 'BinaryCorret', 3087, '发光区二值化补正', 0, NULL, '-20', 14, '2023-11-14 17:45:02', 1, 0, NULL),(3088, 'Boundry', 3088, 'Boundry', 0, NULL, '120', 14, '2023-11-14 17:45:04', 1, 0, NULL),(3089, 'IsuseLocalRdPoint', 3089, 'IsuseLocalRdPoint', 2, NULL, 'false', 14, '2023-11-22 16:24:02', 1, 0, NULL),(3090, 'Picwid', 3090, 'Picwid', 0, NULL, '24', 14, '2023-11-14 17:45:06', 1, 0, NULL),(3091, 'Pichig', 3091, 'Pichig', 0, NULL, '32', 14, '2023-11-14 17:45:08', 1, 0, NULL),(3092, 'LengthCheck', 3092, 'LengthCheck', 5, NULL, '10,10,10,10', 14, '2023-11-14 17:46:49', 1, 0, NULL),(3093, 'LengthRange', 3093, 'LengthRange', 5, NULL, '10,10,10,10', 14, '2023-11-14 17:46:54', 1, 0, NULL),(3094, 'LocalRdMark', 3094, 'LocalRdMark', 5, NULL, '10,10,10,10', 14, '2023-11-14 17:46:57', 1, 0, NULL);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_sys_dictionary_mod_master` WRITE;
DELETE FROM `cv`.`t_scgd_sys_dictionary_mod_master`;
INSERT INTO `cv`.`t_scgd_sys_dictionary_mod_master` (`id`,`code`,`name`,`pid`,`create_date`,`is_enable`,`is_delete`,`remark`,`tenant_id`) VALUES (1, 'camera', '相机', NULL, '2023-07-05 16:38:09', 1, 0, NULL, 0),(2, 'Calibration', '校正', NULL, '2023-12-01 15:56:41', 1, 0, NULL, 0),(3, 'pg', 'PG', NULL, '2023-07-05 10:33:38', 1, 0, NULL, 0),(4, 'auto_exp_time', '自动曝光', NULL, '2023-07-05 10:33:39', 1, 0, NULL, 0),(5, 'centre_line', '中心线', NULL, '2023-07-05 10:33:39', 1, 0, NULL, 0),(6, 'FOV', 'FOV', NULL, '2023-07-05 10:33:40', 1, 0, NULL, 0),(7, 'ghost', '鬼影', NULL, '2023-07-05 10:33:41', 1, 0, NULL, 0),(8, 'MTF', 'MTF', NULL, '2023-07-05 10:33:41', 1, 0, NULL, 0),(9, 'SFR', 'SFR', NULL, '2023-07-05 10:33:42', 1, 0, NULL, 0),(10, 'distortion', '畸变计算', NULL, '2023-07-05 10:33:42', 1, 0, NULL, 0),(11, 'flow', '流程', NULL, '2023-07-05 16:37:10', 1, 0, NULL, 0),(12, 'AOI', 'AOI', NULL, '2023-07-06 15:52:12', 1, 0, NULL, 0),(13, 'SMU', '源表', NULL, '2023-08-18 09:40:04', 1, 0, NULL, 0),(14, 'ledcheck', 'LedCheck', NULL, '2023-11-14 11:38:56', 1, 0, NULL, 0),(15, 'focusPoints', 'FocusPoints', NULL, '2023-11-14 11:38:58', 1, 0, NULL, 0);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_sys_mqtt_cfg` WRITE;
DELETE FROM `cv`.`t_scgd_sys_mqtt_cfg`;
INSERT INTO `cv`.`t_scgd_sys_mqtt_cfg` (`id`,`name`,`host`,`port`,`user`,`password`,`endpoint`,`create_date`,`tenant_id`,`is_enable`,`is_delete`,`remark`) VALUES (1, '192.168.3.225_1883', '192.168.3.225', 1883, NULL, NULL, '@tcp://0.0.0.0:5556', '2023-09-05 17:47:39', 0, 1, 0, NULL),(2, '127.0.0.1_1883', '127.0.0.1', 1883, NULL, NULL, '@tcp://0.0.0.0:5556', '2023-09-05 17:47:41', 0, 0, 0, NULL);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_sys_resource` WRITE;
DELETE FROM `cv`.`t_scgd_sys_resource`;
INSERT INTO `cv`.`t_scgd_sys_resource` (`id`,`name`,`code`,`type`,`pid`,`value`,`txt_value`,`create_date`,`tenant_id`,`is_enable`,`is_delete`,`remark`) VALUES (1, '123', '123', 8, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"CfwPort/STATUS/123\",\"SendTopic\":\"CfwPort/CMD/123\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-01 14:54:17', 0, 1, 0, NULL),(2, '123', '123', 1, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"camera/STATUS/123\",\"SendTopic\":\"camera/CMD/123\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-01 15:50:53', 0, 1, 1, NULL),(3, 'cs01', 'cs01', 1, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-01 15:51:15', 0, 1, 0, NULL),(4, 'BV2000', 'e58adc4ea51efbbf9', 1, 3, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"ChannelConfigs\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"szComName\":null,\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0.0,\"dMinValue\":0.0}},\"ID\":\"e58adc4ea51efbbf9\",\"SNID\":\"e58adc4ea51efbbf9\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"LV2000\",\"Code\":\"e58adc4ea51efbbf9\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-01T15:52:01.966573+08:00\"}', '2023-11-03 16:34:16', 0, 1, 1, NULL),(5, '123', '13', 9, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"Calibration/STATUS/13\",\"SendTopic\":\"Calibration/CMD/13\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-01 16:31:30', 0, 1, 0, NULL),(6, '123', '123', 9, 5, NULL, '{\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"ID\":\"123\",\"SNID\":\"123\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"Calibration/STATUS/13\",\"SendTopic\":\"Calibration/CMD/13\",\"Name\":\"123\",\"Code\":\"123\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-01 16:31:41', 0, 1, 0, NULL),(7, 'qk', 'qk', 7, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"Algorithm/STATUS/qk\",\"SendTopic\":\"Algorithm/CMD/qk\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-02 17:50:21', 0, 1, 0, NULL),(8, 'qkqq', 'qkqq', 7, 7, NULL, '{\"Endpoint\":\"tcp://127.0.0.1:6633\",\"CIEFileBasePath\":null,\"RawFileBasePath\":null,\"SrcFileBasePath\":null,\"ID\":\"qkqq\",\"SNID\":\"qkqq\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"Algorithm/STATUS/qk\",\"SendTopic\":\"Algorithm/CMD/qk\",\"Name\":\"qkqq\",\"Code\":\"qkqq\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-02T17:51:33.6306868+08:00\"}', '2023-11-02 17:51:10', 0, 1, 0, NULL),(9, '213', '23', 11, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"FocusRing/STATUS/23\",\"SendTopic\":\"FocusRing/CMD/23\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-03 14:24:01', 0, 1, 0, NULL),(10, '213', '213', 11, 9, NULL, '{\"eFOCUSCOMMUN\":0,\"szComName\":null,\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0.0,\"dMinValue\":0.0},\"Position\":0,\"ID\":\"213\",\"SNID\":\"213\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"FocusRing/STATUS/23\",\"SendTopic\":\"FocusRing/CMD/23\",\"Name\":\"213\",\"Code\":\"213\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-03 14:34:14', 0, 1, 0, NULL),(11, 'qk', 'qk', 1, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-08 11:47:29', 0, 1, 1, NULL),(12, 'qkc', 'qkc', 1, 11, NULL, '{\"CameraType\":2,\"TakeImageMode\":0,\"ImageBpp\":16,\"Channel\":3,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"ChannelConfigs\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":0,\"ChannelType\":1},{\"Port\":1,\"ChannelType\":2}],\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"szComName\":null,\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0.0,\"dMinValue\":0.0}},\"ID\":\"qkc\",\"SNID\":\"qkc\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":\"qkc\",\"Code\":\"qkc\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-08T11:43:28.227623+08:00\"}', '2023-11-08 11:47:27', 0, 1, 1, NULL),(13, '123', '13', 10, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"Motor/STATUS/13\",\"SendTopic\":\"Motor/CMD/13\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-03 15:05:33', 0, 1, 0, NULL),(14, '13', '123', 10, 13, NULL, '{\"eFOCUSCOMMUN\":2,\"szComName\":null,\"BaudRate\":11520005,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0.0,\"dMinValue\":0.0},\"Position\":0,\"ID\":\"123\",\"SNID\":\"123\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"Motor/STATUS/13\",\"SendTopic\":\"Motor/CMD/13\",\"Name\":\"13\",\"Code\":\"123\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 14:38:54', 0, 1, 1, NULL),(15, '234234', '24324', 8, 1, NULL, '{\"SzComName\":null,\"BaudRate\":9600,\"ID\":\"24324\",\"SNID\":\"24324\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"CfwPort/STATUS/123\",\"SendTopic\":\"CfwPort/CMD/123\",\"Name\":\"234234\",\"Code\":\"24324\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-03 15:15:43', 0, 1, 0, NULL),(16, 'LV-2000', 'e29b14429bc375b1', 1, 3, NULL, '{\"CameraType\":0,\"TakeImageMode\":0,\"ImageBpp\":0,\"Channel\":0,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"ChannelConfigs\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":null,\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":null,\"SNID\":null,\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":null,\"SendTopic\":null,\"Name\":\"213\",\"Code\":\"e29b14429bc375b1\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 10:32:06', 0, 1, 1, NULL),(17, '123', '123', 4, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"SMU/STATUS/123\",\"SendTopic\":\"SMU/CMD/123\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-07 11:29:14', 0, 1, 0, NULL),(18, '213', '213', 2, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"pg/STATUS/213\",\"SendTopic\":\"pg/CMD/213\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-07 11:34:50', 0, 1, 0, NULL),(19, '13', '13', 5, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"sensor/STATUS/13\",\"SendTopic\":\"sensor/CMD/13\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-08 10:41:08', 0, 1, 0, NULL),(20, '123', '123', 5, 19, NULL, '{\"CommunicateType\":0,\"SzIPAddress\":null,\"Port\":0,\"ID\":\"123\",\"SNID\":\"123\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"sensor/STATUS/13\",\"SendTopic\":\"sensor/CMD/13\",\"Name\":\"123\",\"Code\":\"123\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-08 10:41:14', 0, 1, 0, NULL),(21, 'vvvvvv', 'e29b14429bc375b11', 1, 3, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"ChannelConfigs\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"szComName\":null,\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0.0,\"dMinValue\":0.0}},\"ID\":\"e29b14429bc375b11\",\"SNID\":\"e29b14429bc375b11\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"vvvvvv\",\"Code\":\"e29b14429bc375b11\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-08 11:32:54', 0, 1, 1, NULL),(22, 'qk', 'qk', 1, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 15:16:29', 0, 1, 1, NULL),(23, '566b2242984bc686b', '566b2242984bc686b', 1, 22, NULL, '{\"CameraType\":2,\"TakeImageMode\":3,\"ImageBpp\":16,\"Channel\":3,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"ChannelConfigs\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":0,\"ChannelType\":1},{\"Port\":0,\"ChannelType\":2}],\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"szComName\":null,\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0.0,\"dMinValue\":0.0}},\"ID\":\"566b2242984bc686b\",\"SNID\":\"566b2242984bc686b\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":\"566b2242984bc686b\",\"Code\":\"566b2242984bc686b\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-08T11:48:15.7752581+08:00\"}', '2023-11-09 11:58:46', 0, 1, 1, NULL),(24, '00DA0076428', '00DA0076428', 1, 3, NULL, '{\"CameraType\":5,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"ChannelConfigs\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsHaveMotor\":true,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM3\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0.0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"00DA0076428\",\"SNID\":\"00DA0076428\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"00DA0076428\",\"Code\":\"00DA0076428\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-09T10:19:30.6413304+08:00\"}', '2023-11-09 10:32:04', 0, 1, 1, NULL),(25, 'e29b14429bc375b1', 'e29b14429bc375b1', 1, 3, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"IsCOM\":false,\"SzComName\":null,\"BaudRate\":9600,\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}]},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":3,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"e29b14429bc375b1\",\"SNID\":\"e29b14429bc375b1\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"e29b14429bc375b1\",\"Code\":\"e29b14429bc375b1\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-09T11:03:35.2389653+08:00\"}', '2023-11-09 11:34:06', 0, 1, 1, NULL),(26, 'e29b14429bc375b1', '00DA0076428', 1, 3, NULL, '{\"CameraType\":5,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":true,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM3\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"00DA0076428\",\"SNID\":\"00DA0076428\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"e29b14429bc375b1\",\"Code\":\"00DA0076428\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-09T11:32:43.2216894+08:00\"}', '2023-11-09 11:34:00', 0, 1, 1, NULL),(27, 'e29b14429bc375b1', '00DA0076428', 1, 3, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"00DA0076428\",\"SNID\":\"00DA0076428\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"00DA0076428\",\"Code\":\"00DA0076428\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-09T11:34:16.2160983+08:00\"}', '2023-11-09 11:38:44', 0, 1, 1, NULL),(28, 'e29b14429bc375b1', 'e29b14429bc375b1', 1, 3, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"e29b14429bc375b1\",\"SNID\":\"e29b14429bc375b1\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"e29b14429bc375b1\",\"Code\":\"e29b14429bc375b1\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 15:35:28', 0, 1, 1, NULL),(29, '00DA0076428', '00DA0076428', 1, 3, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"00DA0076428\",\"SNID\":\"00DA0076428\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"00DA0076428\",\"Code\":\"00DA0076428\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 11:41:04', 0, 1, 1, NULL),(30, '00DA0076428', '00DA0076428', 1, 3, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"00DA0076428\",\"SNID\":\"00DA0076428\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"00DA0076428\",\"Code\":\"00DA0076428\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 11:41:47', 0, 1, 1, NULL),(31, '00DA0076428', '00DA0076428', 1, 3, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"00DA0076428\",\"SNID\":\"00DA0076428\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"00DA0076428\",\"Code\":\"00DA0076428\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-09T11:44:15.4807981+08:00\"}', '2023-11-09 11:44:34', 0, 1, 1, NULL),(32, '00DA0076428', '00DA0076428', 1, 3, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"00DA0076428\",\"SNID\":\"00DA0076428\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"00DA0076428\",\"Code\":\"00DA0076428\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 11:44:45', 0, 1, 1, NULL),(33, '00DA0076428', '00DA0076428', 1, 3, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"00DA0076428\",\"SNID\":\"00DA0076428\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"00DA0076428\",\"Code\":\"00DA0076428\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 11:52:37', 0, 1, 1, NULL),(34, '00DA0076428', '00DA0076428', 1, 3, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"00DA0076428\",\"SNID\":\"00DA0076428\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"00DA0076428\",\"Code\":\"00DA0076428\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 11:53:13', 0, 1, 1, NULL),(35, '00DA0076428', '00DA0076428', 1, 3, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"00DA0076428\",\"SNID\":\"00DA0076428\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"00DA0076428\",\"Code\":\"00DA0076428\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 11:53:52', 0, 1, 1, NULL),(36, '00DA0076428', '00DA0076428', 1, 3, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"00DA0076428\",\"SNID\":\"00DA0076428\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"00DA0076428\",\"Code\":\"00DA0076428\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 11:59:10', 0, 1, 1, NULL),(37, '00DA0076428', '00DA0076428', 1, 3, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"00DA0076428\",\"SNID\":\"00DA0076428\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"00DA0076428\",\"Code\":\"00DA0076428\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 12:00:08', 0, 1, 1, NULL),(38, '00DA0076428', '00DA0076428', 1, 3, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":true,\"MotorConfig\":{\"eFOCUSCOMMUN\":3,\"SzComName\":\"COM1\",\"BaudRate\":1152000,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"00DA0076428\",\"SNID\":\"00DA0076428\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"00DA0076428\",\"Code\":\"00DA0076428\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 15:35:29', 0, 1, 1, NULL),(39, '213', '1', 10, 13, NULL, '{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"Forwardparam\":0.0,\"CurStep\":0,\"Curtailparam\":0.0,\"StopStep\":0,\"MinPosition\":0,\"MaxPosition\":0,\"EvaFunc\":1,\"MinValue\":0.0,\"nTimeout\":30000},\"dwTimeOut\":5000,\"ID\":\"1\",\"SNID\":\"1\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"Motor/STATUS/13\",\"SendTopic\":\"Motor/CMD/13\",\"Name\":\"213\",\"Code\":\"1\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 14:38:57', 0, 1, 0, NULL),(40, 'qk', 'qk', 1, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 15:25:11', 0, 1, 1, NULL),(41, 'qq', 'qq', 1, 40, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"qq\",\"SNID\":\"qq\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":\"qq\",\"Code\":\"qq\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-09 15:22:05', 0, 1, 1, NULL),(42, 'qk', 'qk', 1, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-13 16:24:18', 0, 1, 1, NULL),(43, '566b2242984bc686b', '566b2242984bc686b', 1, 42, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":0},{\"Port\":1,\"ChannelType\":1},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"566b2242984bc686b\",\"SNID\":\"566b2242984bc686b\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":\"566b2242984bc686b\",\"Code\":\"566b2242984bc686b\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-13 16:24:18', 0, 1, 1, NULL),(44, 'e29b14429bc375b1', 'e29b14429bc375b1', 1, 3, NULL, '{\"CameraType\":0,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":3,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":344.0,\"ExpTimeG\":413.0,\"ExpTimeB\":454.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"ChannelCfgs\":[{\"cfwport\":0,\"chtype\":1,\"title\":\"Channel_G\"},{\"cfwport\":2,\"chtype\":0,\"title\":\"Channel_R\"},{\"cfwport\":2,\"chtype\":2,\"title\":\"Channel_B\"}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":115200},\"IsHaveMotor\":true,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":115200,\"AutoFocusConfig\":{\"Forwardparam\":2000.0,\"CurStep\":5000,\"Curtailparam\":0.3,\"StopStep\":200,\"MinPosition\":80000,\"MaxPosition\":180000,\"EvaFunc\":1,\"MinValue\":0.0,\"nTimeout\":30000},\"DwTimeOut\":5000},\"ExpTimeCfg\":{\"autoExpFlag\":true,\"autoExpTimeBegin\":10.0,\"autoExpSyncFreq\":-1.0,\"autoExpSaturation\":70.0,\"autoExpSatMaxAD\":65000,\"autoExpMaxPecentage\":0.01,\"autoExpSatDev\":20.0,\"maxExpTime\":60000.0,\"minExpTime\":0.2,\"burstThreshold\":200.0},\"CameraCfg\":{\"ob\":4,\"obR\":15,\"obT\":154,\"obB\":0,\"tempCtlChecked\":true,\"targetTemp\":-5.0,\"usbTraffic\":50.0,\"offset\":0,\"gain\":10,\"ex\":0,\"ey\":0,\"ew\":0,\"eH\":0},\"ID\":\"e29b14429bc375b1\",\"SNID\":\"e29b14429bc375b1\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"e29b14429bc375b1\",\"Code\":\"e29b14429bc375b1\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-15T17:07:40.6287489+08:00\"}', '2023-11-09 15:35:29', 0, 1, 0, NULL),(45, 'da872647aa85ac896', 'da872647aa85ac896', 1, 42, NULL, '{\"CameraType\":0,\"TakeImageMode\":0,\"ImageBpp\":16,\"Channel\":3,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"CFW\":[{\"Port\":0,\"ChannelType\":1},{\"Port\":1,\"ChannelType\":0},{\"Port\":2,\"ChannelType\":2}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":9600},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":9600,\"AutoFocusConfig\":{\"forwardparam\":0.0,\"curtailparam\":0.0,\"curStep\":0,\"stopStep\":0,\"minPosition\":0,\"maxPosition\":0,\"eEvaFunc\":0,\"dMinValue\":0.0},\"DwTimeOut\":5000},\"ID\":\"da872647aa85ac896\",\"SNID\":\"da872647aa85ac896\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":\"da872647aa85ac896\",\"Code\":\"da872647aa85ac896\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-09T15:38:40.6687918+08:00\"}', '2023-11-13 16:24:18', 0, 1, 1, NULL),(46, 'qk', 'qk', 9, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"Calibration/STATUS/qk\",\"SendTopic\":\"Calibration/CMD/qk\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-10 10:52:19', 0, 1, 0, NULL),(47, '12345', '12345', 9, 46, NULL, '{\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"ID\":\"12345\",\"SNID\":\"12345\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"Calibration/STATUS/qk\",\"SendTopic\":\"Calibration/CMD/qk\",\"Name\":\"12345\",\"Code\":\"12345\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-10T10:54:22.9546777+08:00\"}', '2023-11-10 10:52:44', 0, 1, 0, NULL),(48, 'default1', 'a4f584353b0a20ba63eb79941938b555e4f6587a99d8bddfb091510205612e06', 101, NULL, NULL, '', '0001-01-01 00:00:00', 0, 1, 0, NULL),(49, 'qk', 'qk', 1, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-10 14:58:30', 0, 1, 1, NULL),(50, 'qk', 'qk', 1, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-10 14:58:32', 0, 1, 1, NULL),(51, 'qk', 'qk', 1, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-10 14:58:34', 0, 1, 1, NULL),(52, 'E:\\newGenaral\\Debug\\mil-dcf\\VP101_85Mhz_10tap8bit-con-good.dcf', 'E:\\newGenaral\\Debug\\mil-dcf\\VP101_85Mhz_10tap8bit-con-good.dcf', 1, 42, NULL, '{\"CameraType\":8,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"ChannelCfgs\":[{\"cfwport\":0,\"chtype\":1,\"title\":\"Channel_G\"},{\"cfwport\":1,\"chtype\":0,\"title\":\"Channel_R\"},{\"cfwport\":2,\"chtype\":2,\"title\":\"Channel_B\"}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":115200},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":115200,\"AutoFocusConfig\":{\"Forwardparam\":2000.0,\"CurStep\":5000,\"Curtailparam\":0.3,\"StopStep\":200,\"MinPosition\":80000,\"MaxPosition\":180000,\"EvaFunc\":1,\"MinValue\":0.0,\"nTimeout\":30000},\"DwTimeOut\":5000},\"ExpTimeCfg\":{\"autoExpFlag\":true,\"autoExpTimeBegin\":10.0,\"autoExpSyncFreq\":-1.0,\"autoExpSaturation\":70.0,\"autoExpSatMaxAD\":65000,\"autoExpMaxPecentage\":0.01,\"autoExpSatDev\":20.0,\"maxExpTime\":60000.0,\"minExpTime\":0.2,\"burstThreshold\":200.0},\"CameraCfg\":{\"ob\":4,\"obR\":0,\"obT\":0,\"obB\":0,\"tempCtlChecked\":true,\"targetTemp\":-5.0,\"usbTraffic\":0.0,\"offset\":0,\"gain\":10,\"ex\":0,\"ey\":0,\"ew\":0,\"eH\":0},\"ID\":\"E:\\\\newGenaral\\\\Debug\\\\mil-dcf\\\\VP101_85Mhz_10tap8bit-con-good.dcf\",\"SNID\":\"E:\\\\newGenaral\\\\Debug\\\\mil-dcf\\\\VP101_85Mhz_10tap8bit-con-good.dcf\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":\"E:\\\\newGenaral\\\\Debug\\\\mil-dcf\\\\VP101_85Mhz_10tap8bit-con-good.dcf\",\"Code\":\"E:\\\\newGenaral\\\\Debug\\\\mil-dcf\\\\VP101_85Mhz_10tap8bit-con-good.dcf\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-10T17:04:05.1382176+08:00\"}', '2023-11-13 16:24:18', 0, 1, 1, NULL),(53, 'qk', 'qk', 1, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-13 16:24:21', 0, 1, 1, NULL),(54, 'D:\\git\\scgd_internal_dll\\cvCameraItem\\SDK\\Debug\\mil-dcf\\VP101_85Mhz_10tap8bit-con-good.dcf', 'D:\\git\\scgd_internal_dll\\cvCameraItem\\SDK\\Debug\\mil-dcf\\VP101_85Mhz_10tap8bit-con-good.dcf', 1, 53, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"ChannelCfgs\":[{\"cfwport\":0,\"chtype\":1,\"title\":\"Channel_G\"},{\"cfwport\":1,\"chtype\":0,\"title\":\"Channel_R\"},{\"cfwport\":2,\"chtype\":2,\"title\":\"Channel_B\"}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":115200},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":115200,\"AutoFocusConfig\":{\"Forwardparam\":2000.0,\"CurStep\":5000,\"Curtailparam\":0.3,\"StopStep\":200,\"MinPosition\":80000,\"MaxPosition\":180000,\"EvaFunc\":1,\"MinValue\":0.0,\"nTimeout\":30000},\"DwTimeOut\":5000},\"ExpTimeCfg\":{\"autoExpFlag\":true,\"autoExpTimeBegin\":10.0,\"autoExpSyncFreq\":-1.0,\"autoExpSaturation\":70.0,\"autoExpSatMaxAD\":65000,\"autoExpMaxPecentage\":0.01,\"autoExpSatDev\":20.0,\"maxExpTime\":60000.0,\"minExpTime\":0.2,\"burstThreshold\":200.0},\"CameraCfg\":{\"ob\":4,\"obR\":0,\"obT\":0,\"obB\":0,\"tempCtlChecked\":true,\"targetTemp\":-5.0,\"usbTraffic\":0.0,\"offset\":0,\"gain\":10,\"ex\":0,\"ey\":0,\"ew\":0,\"eH\":0},\"ID\":\"D:\\\\git\\\\scgd_internal_dll\\\\cvCameraItem\\\\SDK\\\\Debug\\\\mil-dcf\\\\VP101_85Mhz_10tap8bit-con-good.dcf\",\"SNID\":\"D:\\\\git\\\\scgd_internal_dll\\\\cvCameraItem\\\\SDK\\\\Debug\\\\mil-dcf\\\\VP101_85Mhz_10tap8bit-con-good.dcf\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":\"D:\\\\git\\\\scgd_internal_dll\\\\cvCameraItem\\\\SDK\\\\Debug\\\\mil-dcf\\\\VP101_85Mhz_10tap8bit-con-good.dcf\",\"Code\":\"D:\\\\git\\\\scgd_internal_dll\\\\cvCameraItem\\\\SDK\\\\Debug\\\\mil-dcf\\\\VP101_85Mhz_10tap8bit-con-good.dcf\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-13 16:24:21', 0, 1, 1, NULL),(55, 'qk', 'qk', 1, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-13 17:21:55', 0, 1, 1, NULL),(56, 'da872647aa85ac896', 'da872647aa85ac896', 1, 55, NULL, '{\"CameraType\":0,\"TakeImageMode\":0,\"ImageBpp\":16,\"Channel\":3,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"ChannelCfgs\":[{\"cfwport\":0,\"chtype\":1,\"title\":\"Channel_G\"},{\"cfwport\":1,\"chtype\":0,\"title\":\"Channel_R\"},{\"cfwport\":2,\"chtype\":2,\"title\":\"Channel_B\"}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":115200},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":115200,\"AutoFocusConfig\":{\"Forwardparam\":2000.0,\"CurStep\":5000,\"Curtailparam\":0.3,\"StopStep\":200,\"MinPosition\":80000,\"MaxPosition\":180000,\"EvaFunc\":1,\"MinValue\":0.0,\"nTimeout\":30000},\"DwTimeOut\":5000},\"ExpTimeCfg\":{\"autoExpFlag\":true,\"autoExpTimeBegin\":10.0,\"autoExpSyncFreq\":-1.0,\"autoExpSaturation\":70.0,\"autoExpSatMaxAD\":65000,\"autoExpMaxPecentage\":0.01,\"autoExpSatDev\":20.0,\"maxExpTime\":60000.0,\"minExpTime\":0.2,\"burstThreshold\":200.0},\"CameraCfg\":{\"ob\":4,\"obR\":0,\"obT\":0,\"obB\":0,\"tempCtlChecked\":true,\"targetTemp\":-5.0,\"usbTraffic\":0.0,\"offset\":0,\"gain\":10,\"ex\":0,\"ey\":0,\"ew\":0,\"eH\":0},\"ID\":\"da872647aa85ac896\",\"SNID\":\"da872647aa85ac896\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":\"da872647aa85ac896\",\"Code\":\"da872647aa85ac896\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-13T16:25:20.7411067+08:00\"}', '2023-11-13 17:14:16', 0, 1, 1, NULL),(57, '123', '123', 2, 18, NULL, '{\"Category\":null,\"IsNet\":false,\"Addr\":null,\"Port\":0,\"ID\":\"e29b14429bc375b1\",\"SNID\":\"e29b14429bc375b1\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/cs01\",\"SendTopic\":\"camera/CMD/cs01\",\"Name\":\"123\",\"Code\":\"123\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-13T18:11:01.2418689+08:00\"}', '2023-11-13 16:43:57', 0, 1, 0, NULL),(58, 'da872647aa85ac896', 'da872647aa85ac896', 1, 55, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":16,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"ChannelCfgs\":[{\"cfwport\":0,\"chtype\":1,\"title\":\"Channel_G\"},{\"cfwport\":1,\"chtype\":0,\"title\":\"Channel_R\"},{\"cfwport\":2,\"chtype\":2,\"title\":\"Channel_B\"}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":115200},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":115200,\"AutoFocusConfig\":{\"Forwardparam\":2000.0,\"CurStep\":5000,\"Curtailparam\":0.3,\"StopStep\":200,\"MinPosition\":80000,\"MaxPosition\":180000,\"EvaFunc\":1,\"MinValue\":0.0,\"nTimeout\":30000},\"DwTimeOut\":5000},\"ExpTimeCfg\":{\"autoExpFlag\":true,\"autoExpTimeBegin\":10.0,\"autoExpSyncFreq\":-1.0,\"autoExpSaturation\":70.0,\"autoExpSatMaxAD\":65000,\"autoExpMaxPecentage\":0.01,\"autoExpSatDev\":20.0,\"maxExpTime\":60000.0,\"minExpTime\":0.2,\"burstThreshold\":200.0},\"CameraCfg\":{\"ob\":4,\"obR\":0,\"obT\":0,\"obB\":0,\"tempCtlChecked\":true,\"targetTemp\":-5.0,\"usbTraffic\":0.0,\"offset\":0,\"gain\":10,\"ex\":0,\"ey\":0,\"ew\":0,\"eH\":0},\"ID\":\"da872647aa85ac896\",\"SNID\":\"da872647aa85ac896\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":\"da872647aa85ac896\",\"Code\":\"da872647aa85ac896\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-13T17:14:38.5989424+08:00\"}', '2023-11-13 17:21:55', 0, 1, 1, NULL),(59, 'qk', 'qk', 1, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-13 17:23:32', 0, 1, 1, NULL),(60, 'qk', 'qk', 1, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-13 17:23:42', 0, 1, 0, NULL),(61, 'da872647aa85ac896', 'da872647aa85ac896', 1, 60, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":16,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"ChannelCfgs\":[{\"cfwport\":0,\"chtype\":1,\"title\":\"Channel_G\"},{\"cfwport\":1,\"chtype\":0,\"title\":\"Channel_R\"},{\"cfwport\":2,\"chtype\":2,\"title\":\"Channel_B\"}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":115200},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":115200,\"AutoFocusConfig\":{\"Forwardparam\":2000.0,\"CurStep\":5000,\"Curtailparam\":0.3,\"StopStep\":200,\"MinPosition\":80000,\"MaxPosition\":180000,\"EvaFunc\":1,\"MinValue\":0.0,\"nTimeout\":30000},\"DwTimeOut\":5000},\"ExpTimeCfg\":{\"autoExpFlag\":true,\"autoExpTimeBegin\":10.0,\"autoExpSyncFreq\":-1.0,\"autoExpSaturation\":70.0,\"autoExpSatMaxAD\":65000,\"autoExpMaxPecentage\":0.01,\"autoExpSatDev\":20.0,\"maxExpTime\":60000.0,\"minExpTime\":0.2,\"burstThreshold\":200.0},\"CameraCfg\":{\"ob\":4,\"obR\":0,\"obT\":0,\"obB\":0,\"tempCtlChecked\":true,\"targetTemp\":-5.0,\"usbTraffic\":0.0,\"offset\":0,\"gain\":10,\"ex\":0,\"ey\":0,\"ew\":0,\"eH\":0},\"ID\":\"da872647aa85ac896\",\"SNID\":\"da872647aa85ac896\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":\"da872647aa85ac896\",\"Code\":\"da872647aa85ac896\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-13T17:23:57.032083+08:00\"}', '2023-11-13 17:31:15', 0, 1, 1, NULL),(62, 'da872647aa85ac896', 'da872647aa85ac896', 1, 60, NULL, '{\"CameraType\":0,\"TakeImageMode\":0,\"ImageBpp\":16,\"Channel\":3,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":987.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"ChannelCfgs\":[{\"cfwport\":0,\"chtype\":1,\"title\":\"Channel_G\"},{\"cfwport\":1,\"chtype\":0,\"title\":\"Channel_R\"},{\"cfwport\":2,\"chtype\":2,\"title\":\"Channel_B\"}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":115200},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":115200,\"AutoFocusConfig\":{\"Forwardparam\":2000.0,\"CurStep\":5000,\"Curtailparam\":0.3,\"StopStep\":200,\"MinPosition\":80000,\"MaxPosition\":180000,\"EvaFunc\":1,\"MinValue\":0.0,\"nTimeout\":30000},\"DwTimeOut\":5000},\"ExpTimeCfg\":{\"autoExpFlag\":true,\"autoExpTimeBegin\":10.0,\"autoExpSyncFreq\":-1.0,\"autoExpSaturation\":70.0,\"autoExpSatMaxAD\":65000,\"autoExpMaxPecentage\":0.01,\"autoExpSatDev\":20.0,\"maxExpTime\":60000.0,\"minExpTime\":0.2,\"burstThreshold\":200.0},\"CameraCfg\":{\"ob\":4,\"obR\":0,\"obT\":0,\"obB\":0,\"tempCtlChecked\":true,\"targetTemp\":15.0,\"usbTraffic\":0.0,\"offset\":0,\"gain\":10,\"ex\":0,\"ey\":0,\"ew\":0,\"eH\":0},\"ID\":\"da872647aa85ac896\",\"SNID\":\"da872647aa85ac896\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":\"da872647aa85ac896\",\"Code\":\"da872647aa85ac896\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"2023-11-23T14:56:02.0088831+08:00\"}', '2023-11-13 17:31:19', 0, 1, 0, NULL),(63, 'qk', 'qk', 3, NULL, NULL, '{\"ServiceType\":0,\"SubscribeTopic\":\"Spectum/STATUS/qk\",\"SendTopic\":\"Spectum/CMD/qk\",\"Name\":null,\"Code\":null,\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-15 14:04:18', 0, 1, 0, NULL),(64, '123123123', '123', 1, 60, NULL, '{\"CameraType\":1,\"TakeImageMode\":0,\"ImageBpp\":8,\"Channel\":1,\"VideoConfig\":{\"Name\":null,\"Host\":\"127.0.0.1\",\"Port\":9002},\"Gain\":10,\"ExpTime\":10.0,\"ExpTimeR\":10.0,\"ExpTimeG\":10.0,\"ExpTimeB\":10.0,\"Saturation\":-1.0,\"SaturationR\":-1.0,\"SaturationG\":-1.0,\"SaturationB\":-1.0,\"CFW\":{\"ChannelCfgs\":[{\"cfwport\":0,\"chtype\":1,\"title\":\"Channel_G\"},{\"cfwport\":1,\"chtype\":0,\"title\":\"Channel_R\"},{\"cfwport\":2,\"chtype\":2,\"title\":\"Channel_B\"}],\"IsCOM\":false,\"SzComName\":\"COM1\",\"BaudRate\":115200},\"IsHaveMotor\":false,\"MotorConfig\":{\"eFOCUSCOMMUN\":0,\"SzComName\":\"COM1\",\"BaudRate\":115200,\"AutoFocusConfig\":{\"Forwardparam\":2000.0,\"CurStep\":5000,\"Curtailparam\":0.3,\"StopStep\":200,\"MinPosition\":80000,\"MaxPosition\":180000,\"EvaFunc\":1,\"MinValue\":0.0,\"nTimeout\":30000},\"DwTimeOut\":5000},\"ExpTimeCfg\":{\"autoExpFlag\":true,\"autoExpTimeBegin\":10.0,\"autoExpSyncFreq\":-1.0,\"autoExpSaturation\":70.0,\"autoExpSatMaxAD\":65000,\"autoExpMaxPecentage\":0.01,\"autoExpSatDev\":20.0,\"maxExpTime\":60000.0,\"minExpTime\":0.2,\"burstThreshold\":200.0},\"CameraCfg\":{\"ob\":4,\"obR\":0,\"obT\":0,\"obB\":0,\"tempCtlChecked\":true,\"targetTemp\":-5.0,\"usbTraffic\":0.0,\"offset\":0,\"gain\":10,\"ex\":0,\"ey\":0,\"ew\":0,\"eH\":0},\"ID\":\"123\",\"SNID\":\"123\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"camera/STATUS/qk\",\"SendTopic\":\"camera/CMD/qk\",\"Name\":\"123123123\",\"Code\":\"123\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-23 14:50:15', 0, 1, 0, NULL),(65, '123123', '123', 4, 17, NULL, '{\"IsNet\":false,\"ID\":\"123\",\"SNID\":\"123\",\"MD5\":null,\"IsOnline\":false,\"DeviceServiceStatus\":0,\"SubscribeTopic\":\"SMU/STATUS/123\",\"SendTopic\":\"SMU/CMD/123\",\"Name\":\"123123\",\"Code\":\"123\",\"HeartbeatTime\":2000,\"LastAliveTime\":\"0001-01-01T00:00:00\"}', '2023-11-28 16:14:53', 0, 1, 0, NULL);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_sys_tenant` WRITE;
DELETE FROM `cv`.`t_scgd_sys_tenant`;
INSERT INTO `cv`.`t_scgd_sys_tenant` (`id`,`name`,`create_date`,`is_enable`,`is_delete`,`remark`) VALUES (1, 'cv', '2023-07-05 12:21:12', 1, 0, NULL);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_sys_user` WRITE;
DELETE FROM `cv`.`t_scgd_sys_user`;
INSERT INTO `cv`.`t_scgd_sys_user` (`id`,`name`,`code`,`pwd`,`create_date`,`is_enable`,`is_delete`,`remark`) VALUES (1, 'admin', 'admin', '111111', '2023-07-05 12:21:56', 1, 0, NULL);
UNLOCK TABLES;
COMMIT;
BEGIN;
LOCK TABLES `cv`.`t_scgd_sys_user2tenant` WRITE;
DELETE FROM `cv`.`t_scgd_sys_user2tenant`;
INSERT INTO `cv`.`t_scgd_sys_user2tenant` (`id`,`user_id`,`tenant_id`) VALUES (1, 1, 1);
UNLOCK TABLES;
COMMIT;
