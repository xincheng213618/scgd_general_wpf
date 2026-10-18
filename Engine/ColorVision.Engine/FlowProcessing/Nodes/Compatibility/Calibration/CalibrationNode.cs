#nullable disable
using ColorVision.Engine.PropertyEditor;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Algorithm;

[STNode("校正", CategoryOrder = 331)]
[STNodeSerializationModel("FlowEngineLib.dll|FlowEngineLib.Algorithm.CalibrationNode")]
public class CalibrationNode : CVBaseServerNode
{
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
