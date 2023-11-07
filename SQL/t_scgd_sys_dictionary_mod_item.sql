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

 Date: 07/11/2023 14:11:18
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for t_scgd_sys_dictionary_mod_item
-- ----------------------------
DROP TABLE IF EXISTS `t_scgd_sys_dictionary_mod_item`;
CREATE TABLE `t_scgd_sys_dictionary_mod_item`  (
  `id` int(11) NOT NULL,
  `symbol` varchar(32) CHARACTER SET utf8 COLLATE utf8_general_ci NULL DEFAULT NULL,
  `address_code` bigint(20) NULL DEFAULT NULL,
  `name` varchar(64) CHARACTER SET utf8 COLLATE utf8_general_ci NULL DEFAULT NULL,
  `val_type` tinyint(2) NULL DEFAULT NULL COMMENT '0:整数;1:浮点;2:布尔;3:字符串;4:枚举',
  `value_range` varchar(64) CHARACTER SET utf8 COLLATE utf8_general_ci NULL DEFAULT NULL,
  `default_val` varchar(64) CHARACTER SET utf8 COLLATE utf8_general_ci NULL DEFAULT NULL,
  `pid` int(11) NULL DEFAULT NULL COMMENT 't_scgd_sys_dictionary_mod_master',
  `create_date` datetime NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `is_enable` tinyint(1) NOT NULL DEFAULT 1,
  `is_delete` tinyint(1) NOT NULL DEFAULT 0,
  `remark` varchar(256) CHARACTER SET utf8 COLLATE utf8_general_ci NULL DEFAULT NULL,
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_pid`(`pid`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8 COLLATE = utf8_general_ci COMMENT = '模块参数项字典' ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Records of t_scgd_sys_dictionary_mod_item
-- ----------------------------
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (101, 'ob', 101, 'OB区左', 0, NULL, '4', 1, '2023-06-27 17:36:20', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (102, 'obR', 102, 'OB区右', 0, NULL, '0', 1, '2023-06-27 17:36:22', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (103, 'obT', 103, 'OB区上', 0, NULL, '0', 1, '2023-06-27 17:36:23', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (104, 'obB', 104, 'OB区下', 0, NULL, '0', 1, '2023-06-27 17:36:25', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (105, 'tempCtlChecked', 105, '温度检测', 2, NULL, 'True', 1, '2023-06-27 17:36:31', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (106, 'targetTemp', 106, '目标温度', 0, NULL, '5', 1, '2023-06-27 17:36:35', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (107, 'usbTraffic', 107, 'USB通讯', 0, NULL, '0', 1, '2023-06-27 17:36:38', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (108, 'offset', 108, '移位', 0, NULL, '0', 1, '2023-06-27 17:36:38', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (109, 'gain', 109, '增益', 0, NULL, '0', 1, '2023-06-27 17:36:42', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (110, 'ex', 110, 'x', 0, NULL, '0', 1, '2023-06-27 17:36:43', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (111, 'ey', 111, 'y', 0, NULL, '0', 1, '2023-06-27 17:36:43', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (112, 'ew', 112, 'width', 0, NULL, '0', 1, '2023-06-27 17:36:44', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (113, 'eh', 113, 'height', 0, NULL, '0', 1, '2023-06-27 17:36:45', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (201, 'darknoise', 201, '暗噪声', 3, NULL, NULL, 2, '2023-06-27 11:25:58', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (202, 'dsnu', 202, 'DSNU', 3, NULL, NULL, 2, '2023-06-27 11:25:55', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (203, 'distortion', 203, '畸变', 3, NULL, NULL, 2, '2023-06-27 11:25:51', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (204, 'defect_wpoint', 204, '缺陷点(亮)', 3, NULL, NULL, 2, '2023-06-27 11:25:47', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (205, 'defect_bpoint', 205, '缺陷点(暗)', 3, NULL, NULL, 2, '2023-06-27 11:25:44', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (206, 'luminance', 206, '亮度', 3, NULL, NULL, 2, '2023-06-27 11:26:11', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (207, 'colorone', 207, '单色', 3, NULL, NULL, 2, '2023-06-27 11:26:17', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (208, 'colorfour', 208, '四色', 3, NULL, NULL, 2, '2023-06-27 11:26:25', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (209, 'colormulti', 209, '多色', 3, NULL, NULL, 2, '2023-06-27 11:26:32', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (210, 'uniformity_y', 210, '均匀场Y(绿)', 3, NULL, NULL, 2, '2023-06-27 11:26:47', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (211, 'uniformity_x', 211, '均匀场X(红)', 3, NULL, NULL, 2, '2023-06-27 11:26:45', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (212, 'uniformity_z', 212, '均匀场Z(蓝)', 3, NULL, NULL, 2, '2023-06-27 16:19:12', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (301, 'CM_StartPG', 301, '开始', 3, NULL, 'start\\r', 3, '2023-08-28 15:37:50', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (302, 'CM_StopPG', 302, '停止', 3, NULL, 'stop\\r', 3, '2023-08-28 15:39:06', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (303, 'CM_ReSetPG', 303, '重置', 3, NULL, 'reset\\r', 3, '2023-08-28 15:39:14', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (304, 'CM_SwitchUpPG', 304, '上', 3, NULL, 'key UP\\r', 3, '2023-08-28 15:39:25', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (305, 'CM_SwitchDownPG', 305, '下', 3, NULL, 'key DN\\r', 3, '2023-08-28 15:40:08', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (306, 'CM_SwitchFramePG', 306, '切指定', 3, NULL, 'pat {0}\\r', 3, '2023-08-28 15:41:17', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (401, 'autoExpTimeBegin', 401, '开始时间', 0, NULL, '10', 4, '2023-06-27 17:33:45', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (402, 'autoExpFlag', 402, '是否启用', 2, NULL, 'True', 4, '2023-06-27 17:33:51', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (403, 'autoExpSyncFreq', 403, '频率同步', 0, NULL, '-1', 4, '2023-06-27 17:34:30', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (404, 'autoExpSaturation', 404, '饱和度', 0, NULL, '70', 4, '2023-06-27 17:34:39', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (405, 'autoExpSatMaxAD', 405, 'AD最大值', 0, NULL, '65000', 4, '2023-06-27 17:34:45', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (406, 'autoExpMaxPecentage', 406, '最大值百分比', 1, NULL, '0.01', 4, '2023-06-27 17:35:18', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (407, 'autoExpSatDev', 407, '饱和度差值', 0, NULL, '20', 4, '2023-06-27 17:35:38', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (408, 'maxExpTime', 408, '最大曝光时间', 1, NULL, '60000', 4, '2023-06-27 17:35:45', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (409, 'minExpTime', 409, '最小曝光时间', 1, NULL, '0.2', 4, '2023-06-27 17:35:54', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (410, 'burstThreshold', 410, 'burst阈值', 0, NULL, '200', 4, '2023-06-27 17:35:58', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (1101, 'filename', 1101, '文件名', 3, NULL, NULL, 11, '2023-07-05 16:41:05', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (1201, 'Left', 1201, '左', 0, NULL, '5', 12, '2023-07-06 17:21:18', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (1202, 'Right', 1202, '右', 0, NULL, '5', 12, '2023-07-06 17:21:19', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (1203, 'Top', 1203, '上', 0, NULL, '5', 12, '2023-07-06 17:21:20', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (1204, 'Bottom', 1204, '下', 0, NULL, '5', 12, '2023-07-06 17:21:21', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (1205, 'BlurSize', 1205, 'BlurSize', 0, NULL, '19', 12, '2023-07-06 17:53:30', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (1206, 'DilateSize', 1206, 'DilateSize', 0, NULL, '5', 12, '2023-07-06 18:09:19', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (1207, 'FilterByContrast', 1207, 'FilterByContrast', 2, NULL, 'True', 12, '2023-07-06 18:16:56', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (1208, 'MaxContrast', 1208, 'MaxContrast', 1, NULL, '1.7', 12, '2023-07-06 18:20:12', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (1209, 'MinContrast', 1209, 'MinContrast', 1, NULL, '0.3', 12, '2023-07-06 18:20:32', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (1301, 'IsSourceV', 1301, '是否电压', 2, NULL, 'True', 13, '2023-08-18 09:42:23', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (1302, 'BeginValue', 1302, '开始值', 1, NULL, '0.0', 13, '2023-08-18 09:43:53', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (1303, 'EndValue', 1303, '结束值', 1, NULL, '5.0', 13, '2023-08-18 09:44:38', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (1304, 'LimitValue', 1304, '限值', 1, NULL, '200.0', 13, '2023-08-18 09:45:26', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (1305, 'Points', 1305, '点数', 0, NULL, '100', 13, '2023-08-18 09:46:37', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3000, 'Gamma', 3000, 'Gamma', 1, NULL, '1.0', 9, '2023-10-10 11:41:33', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3001, 'X', 3001, 'X', 0, NULL, '0', 9, '2023-11-07 11:55:18', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3002, 'Y', 3002, 'Y', 0, NULL, '0', 9, '2023-11-07 11:55:20', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3003, 'Width', 3003, 'Width', 0, NULL, '1000', 9, '2023-10-10 11:42:52', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3004, 'Height', 3004, 'Height', 0, NULL, '1000', 9, '2023-10-10 11:43:19', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3010, 'Radio', 3010, '计算FOV时中心区亮度的百分比多少认为是暗区', 1, NULL, '0.2', 6, '2023-10-09 11:26:29', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3011, 'CameraDegrees', 3011, '相机镜头有效像素对应的角度', 1, NULL, '0.2', 6, '2023-10-09 11:26:30', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3012, 'ThresholdValus', 3012, 'FOV中计算圆心或者矩心时使用的二值化阈值', 0, NULL, '20', 6, '2023-10-09 11:26:30', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3013, 'DFovDist', 3013, '相机镜头使用的有效像素', 1, NULL, '8443', 6, '2023-10-09 11:26:31', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3014, 'FovPattern', 3014, '计算pattern(FovCircle-圆形；FovRectangle-矩形)', 4, NULL, '0', 6, '2023-10-09 11:26:32', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3015, 'FovType', 3015, '计算路线(Horizontal-水平；Vertical-垂直；Leaning-斜向)', 4, NULL, '0', 6, '2023-10-09 11:26:32', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3016, 'Xc', 3016, 'Xc', 1, NULL, '0', 6, '2023-11-07 10:59:49', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3017, 'Yc', 3017, 'Yc', 1, NULL, '0', 6, '2023-11-07 10:59:47', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3018, 'Xp', 3018, 'Xp', 1, NULL, '0', 6, '2023-11-07 10:59:47', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3019, 'Yp', 3019, 'Yp', 1, NULL, '0', 6, '2023-11-07 10:59:48', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3020, 'Ghost_radius', 3020, '待检测鬼影点阵的半径长度(像素)', 0, NULL, '65', 7, '2023-10-09 15:13:27', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3021, 'Ghost_cols', 3021, '待检测鬼影点阵的列数', 0, NULL, '3', 7, '2023-10-09 15:13:44', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3022, 'Ghost_rows', 3022, '待检测鬼影点阵的行数', 0, NULL, '3', 7, '2023-10-09 15:13:43', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3023, 'Ghost_ratioH', 3023, '待检测鬼影的中心灰度百分比上限', 1, NULL, '0.4', 7, '2023-10-09 15:13:49', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3024, 'Ghost_ratioL', 3024, '待检测鬼影的中心灰度百分比下限', 1, NULL, '0.2', 7, '2023-10-09 15:42:14', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3030, 'filterByColor', 3030, '是否使用颜色过滤', 2, NULL, 'true', 10, '2023-10-09 15:43:17', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3031, 'blobColor', 3031, '亮斑255暗斑0', 0, '', '0', 10, '2023-10-09 15:43:49', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3032, 'minThreshold', 3032, '阈值每次间隔值', 1, NULL, '10', 10, '2023-10-09 15:50:20', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3033, 'thresholdStep', 3033, '斑点最小灰度', 1, NULL, '10', 10, '2023-10-09 15:50:40', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3034, 'maxThreshold', 3034, '斑点最大灰度', 1, NULL, '220', 10, '2023-10-09 15:51:20', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3035, 'ifDEBUG', 3035, NULL, 2, NULL, NULL, 10, '2023-10-09 15:51:45', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3036, 'darkRatio', 3036, '暗斑比例', 1, NULL, '0.01', 10, '2023-10-09 15:52:11', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3037, 'contrastRatio', 3037, '对比度比例', 1, NULL, '0.1', 10, '2023-10-09 15:52:34', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3038, 'bgRadius', 3038, '背景半径', 0, NULL, '31', 10, '2023-10-09 15:53:03', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3039, 'minDistBetweenBlobs', 3039, '斑点间隔距离', 1, NULL, '50', 10, '2023-10-09 15:53:29', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3040, 'filterByArea', 3040, '是否使用面积过滤', 0, NULL, 'true', 10, '2023-10-09 15:53:59', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3041, 'minArea', 3041, '斑点最小面积值', 1, NULL, '200', 10, '2023-10-09 15:54:49', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3042, 'maxArea', 3042, '斑点最大面积值', 1, NULL, '10000', 10, '2023-10-09 15:55:13', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3043, 'minRepeatability', 3043, '重复次数认定', 0, NULL, '2', 10, '2023-10-09 15:55:39', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3044, 'filterByCircularity', 3044, '形状控制（圆，方)', 0, NULL, NULL, 10, '2023-10-09 15:57:27', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3045, 'minCircularity', 3045, '', 1, NULL, '0.9', 10, '2023-10-09 15:59:29', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3046, 'maxCircularity', 3046, NULL, 1, NULL, '3.40282346638528859e+38', 10, '2023-10-09 16:04:59', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3047, 'filterByConvexity', 3047, '形状控制（豁口）', 0, NULL, NULL, 10, '2023-10-09 15:59:23', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3048, 'minConvexity', 3048, NULL, 1, NULL, '0.9', 10, '2023-10-09 16:00:00', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3049, 'maxConvexity', 3049, NULL, 1, NULL, '3.40282346638528859e+38', 10, '2023-10-09 16:04:29', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3050, 'filterByInertia', 3050, '形状控制（椭圆度）', 0, NULL, NULL, 10, '2023-10-09 16:00:51', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3051, 'minInertiaRatio', 3051, NULL, 1, NULL, '0.9', 10, '2023-10-09 16:00:51', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3052, 'maxInertiaRatio', 3052, NULL, 1, NULL, '3.40282346638528859e+38', 10, '2023-10-09 16:04:28', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3053, 'X', 3053, 'X', 0, NULL, '0', 10, '2023-10-24 15:04:15', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3054, 'Y', 3054, 'Y', 0, NULL, '0', 10, '2023-10-24 15:04:15', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3055, 'Width', 3055, 'cx', 0, NULL, '16', 10, '2023-10-24 15:31:14', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3056, 'Height', 3056, 'cy', 0, NULL, '11', 10, '2023-10-24 15:31:15', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3060, 'MTF_dRatio', 3000, 'MTF_dRatio', 1, NULL, '0.01', 8, '2023-10-08 11:59:12', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3061, 'EvaFunc', 3061, 'EvaFunc', 4, NULL, 'CalResol', 8, '2023-10-10 10:48:14', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3062, 'dx', 3062, 'dx', 0, NULL, '0', 8, '2023-10-10 10:48:39', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3063, 'dy', 3063, 'dy', 0, NULL, '1', 8, '2023-10-10 10:49:13', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3064, 'ksize', 3064, 'ksize', 0, NULL, '5', 8, '2023-10-10 10:49:14', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3070, 'type', 3070, 'type', 0, NULL, '0', 10, '2023-11-07 14:11:10', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3071, 'sType', 3071, 'sType', 0, NULL, '0', 10, '2023-11-07 14:11:10', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3072, 'lType', 3072, 'lType', 0, NULL, '0', 10, '2023-11-07 14:11:11', 1, 0, NULL);
INSERT INTO `t_scgd_sys_dictionary_mod_item` VALUES (3073, 'dType', 3073, 'dType', 0, NULL, '0', 10, '2023-11-07 14:11:11', 1, 0, NULL);

SET FOREIGN_KEY_CHECKS = 1;
