# scgd_general_wpf

## 🖥️ 支持

**运行环境** NET 6.0 , VS2022 , Win11 
**分辨率**：1920x1080,100%
**项目特性**

- 支持操作系统：Win10,Win11
- 支持开机自启，项目在版本相同的情况下，不支持多开
- 支持主题模式：深色，浅色，跟随系统
- I18n：English, 简体中文，繁体中文，日本語,한국어

[更新日志](CHANGELOG.md)

## 翻译API：

Google apikey  AIzaSyBElxN8V59CXS0ML-Q5YC7Do-Rza8FGawE

Baidu  AppID：20180402000142443

​            SecretKey:bTLryuKSa4vWCJLs0ECO





## 项目结构

#### ColorVision

#### ColorVision.Util

#### ColorVision.Common

#### cvColorVision

## 使用流程

#### MQTT配置

```
 "MQTTConfig": {
    "Name": "127.0.0.1_1883",
    "Host": "127.0.0.1",
    "Port": 1883,
    "UserName": "",
    "UserPwd": ""
  }
```

#### 数据库配置

```
 "MySqlConfig": {
    "Name": "127.0.0.1_3306",
    "Host": "127.0.0.1",
    "Port": 3306,
    "UserName": "root",
    "UserPwd": "123456@cv",
    "Database": "cv"
  }
```

## 软件安装流程

1.安装SDK以及相关的相机驱动，光谱仪驱动，串口驱动

2.安装数据库，以及服务

3.安装本软件。

4.配置MQTT，数据库，注册中心的相关参数，登录用户。

5.配置服务相关信息，配置许可证信息，并重启服务，











