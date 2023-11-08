/*
 Navicat Premium Data Transfer

 Source Server         : 192.168.3.250_3306
 Source Server Type    : MySQL
 Source Server Version : 50742
 Source Host           : 192.168.3.250:3306
 Source Schema         : color_vision

 Target Server Type    : MySQL
 Target Server Version : 50742
 File Encoding         : 65001

 Date: 08/11/2023 10:12:27
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for t_scgd_sys_dictionary
-- ----------------------------
DROP TABLE IF EXISTS `t_scgd_sys_dictionary`;
CREATE TABLE `t_scgd_sys_dictionary`  (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) CHARACTER SET utf8 COLLATE utf8_general_ci NULL DEFAULT NULL,
  `pid` int(11) NULL DEFAULT NULL,
  `key` varchar(255) CHARACTER SET utf8 COLLATE utf8_general_ci NULL DEFAULT NULL,
  `val` int(11) NULL DEFAULT NULL,
  `is_enable` tinyint(1) NOT NULL DEFAULT 1,
  `is_delete` tinyint(1) NOT NULL DEFAULT 0,
  `remark` varchar(255) CHARACTER SET utf8 COLLATE utf8_general_ci NULL DEFAULT NULL,
  `tenant_id` int(11) NULL DEFAULT 0,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `idx_val`(`val`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 17 CHARACTER SET = utf8 COLLATE = utf8_general_ci COMMENT = '系统字典表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of t_scgd_sys_dictionary
-- ----------------------------
INSERT INTO `t_scgd_sys_dictionary` VALUES (1, '服务类别', NULL, 'service_type', NULL, 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary` VALUES (2, '相机', 1, 'camera', 1, 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary` VALUES (3, 'PG', 1, 'pg', 2, 1, 0, 'SensorPlugin.PG.PGDevice, SensorPlugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null', 0);
INSERT INTO `t_scgd_sys_dictionary` VALUES (4, '光谱仪', 1, 'Spectum', 3, 1, 0, 'SpectumPlugin.GCSDevice, SpectumPlugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null', 0);
INSERT INTO `t_scgd_sys_dictionary` VALUES (5, '源表', 1, 'SMU', 4, 1, 0, 'SMUPlugin.SMUDevice, SMUPlugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null', 0);
INSERT INTO `t_scgd_sys_dictionary` VALUES (6, '通用传感器', 1, 'sensor', 5, 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary` VALUES (7, '文件服务', 1, 'FileServer', 6, 1, 0, 'FileServerPlugin.FileServerDevice, FileServerPlugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null', 0);
INSERT INTO `t_scgd_sys_dictionary` VALUES (10, '流程类别', NULL, 'flow_type', NULL, 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary` VALUES (11, '流程模板', 10, 'flow_temp', 101, 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary` VALUES (12, '算法服务', 1, 'Algorithm', 7, 1, 0, 'AlgorithmPlugin.Algorithm.AlgorithmDevice, AlgorithmPlugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null', 0);
INSERT INTO `t_scgd_sys_dictionary` VALUES (13, '滤色轮服务', 1, 'FilterWheel', 8, 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary` VALUES (14, '校正服务', 1, 'Calibration', 9, 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary` VALUES (15, '电机服务', 1, 'Motor', 10, 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary` VALUES (16, '对焦环', 1, 'FocusRing', 11, 1, 0, NULL, 0);

SET FOREIGN_KEY_CHECKS = 1;
