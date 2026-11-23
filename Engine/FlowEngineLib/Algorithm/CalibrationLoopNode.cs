using System.Collections.Generic;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using FlowEngineLib.Node.Algorithm;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Algorithm;

public class CalibrationLoopNode : CVBaseLoopServerNode<CalibrationNodeProperty>
{
	private readonly CalibrationResultBinder bindResult;

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

	public CalibrationLoopNode() : this(null)
	{
	}

	internal CalibrationLoopNode(CalibrationResultBinder bindResult)
		: base("校正.For", "Calibration", "SVR.Calibration.Default", "DEV.Calibration.Default")
	{
		this.bindResult = bindResult;
		operatorCode = "Calibration";
	}

	protected override void DispatchRemoteCommand(CVTransAction trans, MQActionEvent action, CVBaseEventCmd command)
	{
		CalibrationFlowResultTracking.Track(trans.trans_action, action, command, bindResult);
		base.DispatchRemoteCommand(trans, action, command);
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
