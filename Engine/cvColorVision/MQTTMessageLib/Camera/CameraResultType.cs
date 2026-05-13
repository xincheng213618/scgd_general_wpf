using System.ComponentModel;

namespace MQTTMessageLib.Camera;

public enum CameraResultType
{
	[Description("未知")]
	Unkown,
	[Description("获取所有ID")]
	GetAllID,
	[Description("扫描相机ID")]
	Scan,
	[Description("打开")]
	Open,
	[Description("打开视频")]
	OpenLive,
	[Description("关闭")]
	Close,
	[Description("测量")]
	GetData,
	[Description("测量+算法")]
	GetDataAndAlg,
	[Description("设置参数")]
	SetParam,
	[Description("自动曝光")]
	GetAutoExpTime,
	[Description("删除数据")]
	DeleteData,
	[Description("获取温度")]
	GetTemperature,
	[Description("电机打开")]
	Motor_Open,
	[Description("自动对焦")]
	Motor_AutoFocus,
	[Description("电机位置")]
	Motor_GetPosition,
	[Description("对焦环返回原点")]
	Motor_GoHome,
	[Description("对焦环移动")]
	Motor_Move,
	[Description("对焦环Diaphragm移动")]
	Motor_MoveDiaphragm
}
