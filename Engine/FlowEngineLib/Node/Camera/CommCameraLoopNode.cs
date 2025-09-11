using System.Collections.Generic;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Camera;

public class CommCameraLoopNode : CVBaseLoopServerNode<CommCameraNodeProperty>
{
	[STNodeProperty("参数", "Get or set the CommCamera params", false, DescriptorType = typeof(LoopNodePropertyDescriptor<CommCameraNodeProperty, FormCommCameraProperty>))]
	public List<CommCameraNodeProperty> Params
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

	public CommCameraLoopNode()
		: base("通用相机.For", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
	{
		operatorCode = "GetData";
		_MaxTime = 20000;
	}

	protected override object getBaseEventData(CVStartCFC start, CommCameraNodeProperty property)
	{
		return new CommCameraData(property.CamTempName, isWithND: false, bool.Parse(property.IsAutoExpTime), property.ExpTempName, property.CaliTempName, property.POITempName, property.POIFilterTempName, property.POIReviseTempName, string.Empty);
	}
}
