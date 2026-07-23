using System.Collections.Generic;
using FlowEngineLib.Base;
using FlowEngineLib.Node.Algorithm;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Algorithm;

public class CalibrationLoopNode : CVBaseLoopServerNode<CalibrationNodeProperty>
{
	[STNodeProperty("参数", "Get or set the Calibration params", false, DescriptorType = typeof(LoopNodePropertyDescriptor<CalibrationNodeProperty, FormCalibrationProperty>))]
	public List<CalibrationNodeProperty> Params
	{
		get
		{
			return _params;
		}
		set
		{
			_params = value;
			updateUI();
			OnPropertyChanged();
		}
	}

	public CalibrationLoopNode()
		: base("校正.For", "Calibration", "SVR.Calibration.Default", "DEV.Calibration.Default")
	{
		operatorCode = "Calibration";
	}

	protected override object getBaseEventData(CVStartCFC start, CalibrationNodeProperty property)
	{
		AlgorithmPreStepParam param = new AlgorithmPreStepParam();
		getPreStepParam(start, param);
		_ImgFileName = property.ImgFileName;
		_TempName = property.TempName;
		CalibrationData calibrationData = new CalibrationData(property.ExpTempName, param, isSaveCIE: true);
		BuildImageParam(calibrationData);
		return calibrationData;
	}
}
