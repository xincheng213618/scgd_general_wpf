using System;
using System.Collections.Generic;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmLoopNode : CVBaseLoopServerNode<AlgorithmNodeProperty>
{
	[STNodeProperty("参数", "Get or set the Calibration params", false, DescriptorType = typeof(LoopNodePropertyDescriptor<AlgorithmNodeProperty, FormAlgorithmProperty>))]
	public List<AlgorithmNodeProperty> Params
	{
		get
		{
			return _params;
		}
		set
		{
			_params = value;
			updateUI();
		}
	}

	public AlgorithmLoopNode()
		: base("算法.For", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
	}

	protected override void OnCreate()
	{
		base.OnCreate();
	}

	private void setAlgorithmType(AlgorithmType _Algorithm)
	{
		switch (_Algorithm)
		{
		case AlgorithmType.SFR:
			operatorCode = "SFR";
			break;
		case AlgorithmType.FOV:
			operatorCode = "FOV";
			break;
		case AlgorithmType.鬼影:
			operatorCode = "Ghost";
			break;
		case AlgorithmType.畸变:
			operatorCode = "Distortion";
			break;
		case AlgorithmType.灯珠检测:
			operatorCode = "LedCheck";
			break;
		case AlgorithmType.发光区检测:
			operatorCode = "FocusPoints";
			break;
		case AlgorithmType.发光区检测OLED:
			operatorCode = "OLED.GetRIAandPT";
			break;
		case AlgorithmType.灯带检测:
			operatorCode = "LEDStripDetection";
			break;
		}
	}

	protected override object getBaseEventData(CVStartCFC start, AlgorithmNodeProperty property)
	{
		int masterId = -1;
		AlgorithmType algorithmType = (AlgorithmType)Enum.Parse(typeof(AlgorithmType), property.AlgorithmType, ignoreCase: true);
		setAlgorithmType(algorithmType);
		AlgorithmParam obj = new AlgorithmParam
		{
			ImgFileName = property.ImgFileName,
			TemplateParam = new CVTemplateParam
			{
				ID = -1,
				Name = property.TempName
			}
		};
		if (start.Data.ContainsKey("MasterId"))
		{
			masterId = Convert.ToInt32(start.Data["MasterId"]);
		}
		obj.MasterId = masterId;
		return obj;
	}
}
