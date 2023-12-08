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

 Date: 08/12/2023 14:20:53
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for t_scgd_sys_camera
-- ----------------------------
DROP TABLE IF EXISTS `t_scgd_sys_camera`;
CREATE TABLE `t_scgd_sys_camera`  (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `SnID` text CHARACTER SET utf8 COLLATE utf8_general_ci NULL,
  `Value` text CHARACTER SET utf8 COLLATE utf8_general_ci NULL,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1 CHARACTER SET = utf8 COLLATE = utf8_general_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of t_scgd_sys_camera
-- ----------------------------

SET FOREIGN_KEY_CHECKS = 1;
