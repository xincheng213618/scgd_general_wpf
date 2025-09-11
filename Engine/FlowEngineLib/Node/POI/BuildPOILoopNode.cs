using System.Collections.Generic;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.POI;

public class BuildPOILoopNode : CVBaseLoopServerNode<POINodeProperty>
{
	[STNodeProperty("参数", "Get or set the BuildPOI params", false, DescriptorType = typeof(LoopNodePropertyDescriptor<POINodeProperty, FormPOIProperty>))]
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

	public BuildPOILoopNode()
		: base("关注点布点.For", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "BuildPOI";
	}

	protected override object getBaseEventData(CVStartCFC start, POINodeProperty property)
	{
		POITypeData pOITypeData = default(POITypeData);
		pOITypeData.PointType = POIPointTypes.None;
		pOITypeData.Width = 0f;
		pOITypeData.Height = 0f;
		POITypeData poiData = pOITypeData;
		BuildPOIData buildPOIData = new BuildPOIData(property.ImgFileName, -1, property.TempName, property.POIOutput, property.OutputFileName, string.Empty, POIBuildType.Common, poiData, string.Empty, string.Empty, 1024);
		getPreStepParam(start, buildPOIData);
		return buildPOIData;
	}
}
