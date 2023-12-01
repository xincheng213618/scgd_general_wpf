/*
 Navicat Premium Data Transfer

 Source Server         : 192.168.3.250_3306
 Source Server Type    : MySQL
 Source Server Version : 50742
 Source Host           : 192.168.3.250:3306
 Source Schema         : cv

 Target Server Type    : MySQL
 Target Server Version : 50742
 File Encoding         : 65001

 Date: 01/12/2023 16:24:18
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for t_scgd_sys_dictionary_mod_master
-- ----------------------------
DROP TABLE IF EXISTS `t_scgd_sys_dictionary_mod_master`;
CREATE TABLE `t_scgd_sys_dictionary_mod_master`  (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `code` varchar(16) CHARACTER SET utf8 COLLATE utf8_general_ci NULL DEFAULT NULL,
  `name` varchar(64) CHARACTER SET utf8 COLLATE utf8_general_ci NULL DEFAULT NULL,
  `pid` int(11) NULL DEFAULT NULL,
  `create_date` datetime NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `is_enable` tinyint(1) NOT NULL DEFAULT 1,
  `is_delete` tinyint(1) NOT NULL DEFAULT 0,
  `remark` varchar(256) CHARACTER SET utf8 COLLATE utf8_general_ci NULL DEFAULT NULL,
  `tenant_id` int(11) NULL DEFAULT 0,
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_code`(`code`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 17 CHARACTER SET = utf8 COLLATE = utf8_general_ci COMMENT = '模块字典主表' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of t_scgd_sys_dictionary_mod_master
-- ----------------------------
INSERT INTO `t_scgd_sys_dictionary_mod_master` VALUES (1, 'camera', '相机', NULL, '2023-07-05 16:38:09', 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary_mod_master` VALUES (2, 'Calibration', '校正', NULL, '2023-12-01 15:56:41', 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary_mod_master` VALUES (3, 'pg', 'PG', NULL, '2023-07-05 10:33:38', 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary_mod_master` VALUES (4, 'auto_exp_time', '自动曝光', NULL, '2023-07-05 10:33:39', 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary_mod_master` VALUES (5, 'centre_line', '中心线', NULL, '2023-07-05 10:33:39', 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary_mod_master` VALUES (6, 'FOV', 'FOV', NULL, '2023-07-05 10:33:40', 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary_mod_master` VALUES (7, 'ghost', '鬼影', NULL, '2023-07-05 10:33:41', 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary_mod_master` VALUES (8, 'MTF', 'MTF', NULL, '2023-07-05 10:33:41', 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary_mod_master` VALUES (9, 'SFR', 'SFR', NULL, '2023-07-05 10:33:42', 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary_mod_master` VALUES (10, 'distortion', '畸变计算', NULL, '2023-07-05 10:33:42', 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary_mod_master` VALUES (11, 'flow', '流程', NULL, '2023-07-05 16:37:10', 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary_mod_master` VALUES (12, 'AOI', 'AOI', NULL, '2023-07-06 15:52:12', 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary_mod_master` VALUES (13, 'SMU', '源表', NULL, '2023-08-18 09:40:04', 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary_mod_master` VALUES (14, 'ledcheck', 'LedCheck', NULL, '2023-11-14 11:38:56', 1, 0, NULL, 0);
INSERT INTO `t_scgd_sys_dictionary_mod_master` VALUES (15, 'focusPoints', 'FocusPoints', NULL, '2023-11-14 11:38:58', 1, 0, NULL, 0);

SET FOREIGN_KEY_CHECKS = 1;
