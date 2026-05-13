using System.ComponentModel;

namespace CVCommCore;

public enum PhysicalResourceType
{
	[Description("流程文件")]
	FlowFile = 21,
	[Description("物理相机")]
	PhyCamera = 101,
	[Description("物理光谱仪")]
	PhySpectrum = 103,
	[Description("校正")]
	Calibration = 9,
	[Description("资源组")]
	ResGroup = 1000
}
