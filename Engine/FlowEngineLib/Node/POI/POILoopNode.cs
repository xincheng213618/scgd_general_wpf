using System.Collections.Generic;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.POI;

public class POILoopNode : CVBaseLoopServerNode<POINodeProperty>
{
	[STNodeProperty("参数", "Get or set the POI params", false, DescriptorType = typeof(LoopNodePropertyDescriptor<POINodeProperty, FormPOIProperty>))]
	public List<POINodeProperty> Params
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

	public POILoopNode()
		: base("关注点算法.For", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "POI";
	}

	protected override object getBaseEventData(CVStartCFC start, POINodeProperty property)
	{
		AlgorithmPreStepParam param = new AlgorithmPreStepParam();
		getPreStepParam(start, param);
		return new POIDataParam(property.ImgFileName, -1, property.TempName, property.FilterTempName, property.ReviseTempName, property.OutputTempName, param, isSubPixel: false, isCCTWave: true);
	}
}
