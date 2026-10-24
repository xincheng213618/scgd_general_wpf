#nullable disable
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Calibration;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Algorithm;

[STNode("相机", CategoryOrder = 200, NodeOrder = 0)]
[STNodeSerializationModel("FlowEngineLib.dll|FlowEngineLib.Algorithm.CalibrationNode")]
[FlowNodeDocumentation("执行图像校正，按校正服务的“使用本地校正”开关选择内部调用或原服务。",
    Notes = "保留原参数、端口和结果类型。本地分支使用输入图像曝光及校正模板，等待内部调用完成；失败不补发服务请求。")]
public class CalibrationNode : CVBaseServerNode
{
	public override bool RequiresRemoteService => !LocalCalibrationFlowRouting.UsesLocalCalibration(DeviceCode);

	protected override FlowLocalExecution CreateLocalExecution(CVMQTTRequest request)
	{
		if (!LocalCalibrationFlowRouting.UsesLocalCalibration(request.DeviceCode)) return null;
		CalibrationData data = (CalibrationData)request.Data;
		return new LocalCalibrationNode { DeviceCode = request.DeviceCode, CalibTempName = data.TemplateParam?.Name ?? string.Empty }
			.CreateCompatibilityExecution(request, data.ImgFileName);
	}

	[STNodeProperty("参数模板", "参数模板", true)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(CalibrationTemplatePropertiesEditor))]
	public string TempName
	{
		get
		{
			return _TempName;
		}
		set
		{
			setTempName(value);
			OnPropertyChanged();
		}
	}

	[STNodeProperty("图像文件", "图像文件", true)]
	[System.ComponentModel.DataAnnotations.Display(Order = -100)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(System.ComponentModel.TextSelectFilePropertiesEditor))]
	public string ImgFileName
	{
		get
		{
			return _ImgFileName;
		}
		set
		{
			_ImgFileName = value;
			OnPropertyChanged();
		}
	}

	public CalibrationNode()
		: base("校正", "Calibration", "SVR.Calibration.Default", "DEV.Calibration.Default")
	{
		operatorCode = "Calibration";
		_MaxTime = 10000;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item, "校正模板:");
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam param = new AlgorithmPreStepParam();
		getPreStepParam(start, param);
		CalibrationData calibrationData = new CalibrationData(string.Empty, param, isSaveCIE: true);
		BuildImageParam(calibrationData);
		return calibrationData;
	}
}
