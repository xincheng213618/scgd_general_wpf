# scgd_general_wpf

项目开发环境，NET 6.0 , WPF, C# , VS2022 企业版, Win11 
默认分辨率：1920x1080
项目在版本相同的情况下，不支持多开

支持操作系统：Win10,Win11

支持主题：深色，浅色，跟随系统

支持语言：简体中文，繁体中文，韩语，日本语，英语（美式）

支持开机自启

## 项目结构

### ColorVision

项目主程序

### ColorVision.Util

一些可以通用的代码剥离

### cvColorVision

调用原始工程中C++的相关代码

## 使用流程

#### MQTT配置

```
 "MQTTConfig": {
    "Name": "192.168.3.225_1883",
    "Host": "192.168.3.225",
    "Port": 1883,
    "UserName": "",
    "UserPwd": ""
  }
```

#### 数据库配置

```
 "MySqlConfig": {
    "Name": "192.168.3.250_3306",
    "Host": "192.168.3.250",
    "Port": 3306,
    "UserName": "root",
    "UserPwd": "123456@cv",
    "Database": "cv"
  }
```

## 流程

1、进行各种驱动、数据库、MQTT、主程序安装，初始化工作完成（包含配置文件、数据库脚本等）--------------有可能只有主程序
2、启动主程序，配置系统相关参数（包括数据库、MQTT等参数），使用管理员对用户名和租户进行建权，使用用户名进行登录（拿到对应的权限和租户）
3、在服务配置界面，根据权限（超级管理员所有功能都有）分为注册（set）或者发现（get）所有的服务，并拿到这些服务下当前的硬件资源id，并进行选择，完成服务的初始化。超级管理员还需要分配租户（现在可以视作服务）及对租户中的用户进行配置，用户可以看到自己所属租户的所有资源
4、在设备配置界面，用户根据租户拿到所有的设备，进行选择并进行初始化（其实就是独占了资源）、取别名等操作，首先验证license，完成后摆放到界面上以供使用。

## 程序流程细化



### 管理员管理服务

1 .服务的增删改查（id、别名、服务参数、配置TOPIC。。）

2.设备的增删改查（id、别名、设备参数，sn , 许可证）

3.现在的用户无法进行设备的增删改查，是由管理员来分配的

### 服务操作

### 操作逻辑

初始化服务（获取所有的该服务下的SN）

服务心跳

以现在数据库中的列别来呈现设备种类

用户可以修改别名、及其相关的一些属性（设备模板绑定），以相机为例

1、初始化设备（发送协议，带SN）
2、注销设备（发送协议，带SN）
3、设备心跳	

### 主界面

根据设备配置的生成对应的界面列表，并执行对应的操作



## MQTT协议

## 心跳协议

```
{
	"Code" : 0,
	"EventName" : "Heartbeat",
	"MsgID" : 0,
	"ServiceID" : 0,
	"ServiceName" : "Camera",
	"data" : ""
}
```

```
{
	"Code" : 0,
	"EventName" : "Heartbeat",
	"MsgID" : 0,
	"ServiceID" : 0,
	"ServiceName" : "PG",
	"data" : ""
}
```

```
{
	"Code" : 0,
	"EventName" : "Heartbeat",
	"MsgID" : 0,
	"ServiceID" : 0,
	"ServiceName" : "SMU",
	"data" : ""
}
```

```
{
	"Code" : 0,
	"EventName" : "Heartbeat",
	"MsgID" : 0,
	"ServiceID" : 0,
	"ServiceName" : "Camera",
	"data" : ""
}
```

```
{
	"Code" : 0,
	"EventName" : "Heartbeat",
	"MsgID" : 0,
	"ServiceID" : 0,
	"ServiceName" : "Spectrum",
	"data" : ""
}
```

```
{
	"Code" : 0,
	"EventName" : "Heartbeat",
	"MsgID" : 0,
	"ServiceID" : 0,
	"ServiceName" : "Sensor",
	"data" : ""
}
```

### 相机协议

#### Init

```
 "MsgSend": {
      "Version": "1.0",
      "EventName": "Init",
      "ServiceName": "Camera",
      "ServiceID": 2311455622160,
      "SnID": "e29b14429bc375b1",
      "MsgID": "1f794554-ca41-4edc-aa84-5119925b0771",
      "params": {
        "CameraType": 1
      }
    },
    "MsgReturn": {
      "Version": null,
      "EventName": "Init",
      "ServiceName": "Camera",
      "ServiceID": 2311456371280,
      "SnID": "e29b14429bc375b1",
      "Code": 0,
      "MsgID": "1f794554-ca41-4edc-aa84-5119925b0771",
      "data": {
        "SnID": "{\n\t\"ID\" : \n\t[\n\t\t\"e29b14429bc375b1\"\n\t],\n\t\"number\" : 1\n}\n"
      }
    }
```

#### UnInit

```
 "MsgSend": {
      "Version": "1.0",
      "EventName": "UnInit",
      "ServiceName": "Camera",
      "ServiceID": 2311455622160,
      "SnID": "e29b14429bc375b1",
      "MsgID": "5d45d13a-6673-4036-85d8-4633914217b0",
      "params": null
    },
    "MsgReturn": {
      "Version": null,
      "EventName": "UnInit",
      "ServiceName": "Camera",
      "ServiceID": 2311455622160,
      "SnID": "e29b14429bc375b1",
      "Code": 1,
      "MsgID": "5d45d13a-6673-4036-85d8-4633914217b0",
      "data": ""
    }
```

#### Open

```
 "MsgSend": {
      "Version": "1.0",
      "EventName": "Open",
      "ServiceName": "Camera",
      "ServiceID": 2311456371280,
      "SnID": "e29b14429bc375b1",
      "MsgID": "1665662a-859c-423d-b156-e8bbf1a540ae",
      "params": {
        "TakeImageMode": 0,
        "SnID": "e29b14429bc375b1",
        "Bpp": 8
      }
    },
    "MsgReturn": {
      "Version": null,
      "EventName": "Open",
      "ServiceName": "Camera",
      "ServiceID": 2311456371280,
      "SnID": "e29b14429bc375b1",
      "Code": 1,
      "MsgID": "1665662a-859c-423d-b156-e8bbf1a540ae",
      "data": ""
    },
```

#### Close

```
 "MsgSend": {
      "Version": "1.0",
      "EventName": "Close",
      "ServiceName": "Camera",
      "ServiceID": 2091665652640,
      "SnID": "e29b14429bc375b1",
      "MsgID": "d27b6a89-b21f-452a-9941-af96721ec1bc",
      "params": null
    },
    "MsgReturn": {
      "Version": null,
      "EventName": "Close",
      "ServiceName": "Camera",
      "ServiceID": 2091665652640,
      "SnID": "e29b14429bc375b1",
      "Code": 0,
      "MsgID": "d27b6a89-b21f-452a-9941-af96721ec1bc",
      "data": ""
    }
```

#### GetAutoExptime

```
    "MsgSend": {
      "Version": "1.0",
      "EventName": "GetAutoExpTime",
      "ServiceName": "Camera",
      "ServiceID": 2227923805216,
      "SnID": "e29b14429bc375b1",
      "MsgID": "23891bc0-86fe-469f-ac75-c487d13dd424",
      "params": {
        "SetCfwport": [
          {
            "nIndex": 0,
            "nPort": 2,
            "eImgChlType": 0
          },
          {
            "nIndex": 1,
            "nPort": 2,
            "eImgChlType": 0
          },
          {
            "nIndex": 2,
            "nPort": 2,
            "eImgChlType": 0
          }
        ]
      }
    },
    "MsgReturn": {
      "Version": null,
      "EventName": "GetAutoExpTime",
      "ServiceName": "Camera",
      "ServiceID": 2227923805216,
      "SnID": "e29b14429bc375b1",
      "Code": 0,
      "MsgID": "23891bc0-86fe-469f-ac75-c487d13dd424",
      "data": {
        "result": [
          {
            "result": 60000.0,
            "resultSaturation": 0.0
          },
          {
            "result": 0.0,
            "resultSaturation": 0.0
          },
          {
            "result": 0.0,
            "resultSaturation": 0.0
          }
        ]
      }
    }
```

#### GetData

```
"MsgSend": {
      "Version": "1.0",
      "EventName": "GetData",
      "ServiceName": "Camera",
      "ServiceID": 2890885791520,
      "SnID": "e29b14429bc375b1",
      "MsgID": "4f5c214a-68ef-48f3-a353-9cd6dc1d6b26",
      "params": {
        "expTime": 100.0,
        "gain": 1.0,
        "savefilename": "1.tif"
      }
    },
    "MsgReturn": {
      "Version": null,
      "EventName": "GetData",
      "ServiceName": "Camera",
      "ServiceID": 2890885791520,
      "SnID": "e29b14429bc375b1",
      "Code": 1,
      "MsgID": "4f5c214a-68ef-48f3-a353-9cd6dc1d6b26",
      "data": {
        "SaveFileName": "C:\\Users\\17917\\Desktop\\scgd_general_wpf\\ColorVision\\bin\\x64\\Debug\\net6.0-windows\\TIF\\1.tif"
      }
    }
```

### PG协议
