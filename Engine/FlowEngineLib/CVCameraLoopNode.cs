using System;
using System.Collections.Generic;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.Node.Camera;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

internal class CVCameraLoopNode : CVBaseLoopServerNode<CVCameraNodeProperty>
{
	[STNodeProperty("参数", "Get or set the CVCamera params", false, DescriptorType = typeof(LoopNodePropertyDescriptor<CVCameraNodeProperty, FormCVCameraProperty>))]
	public List<CVCameraNodeProperty> Params
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

	public CVCameraLoopNode()
		: base("CV相机.For", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
	{
		operatorCode = "GetData";
		_MaxTime = 20000;
	}

	protected override int GetMaxDelay()
	{
		if (idx < _params.Count)
		{
			return base.GetMaxDelay() + Convert.ToInt32(_params[idx].TempR) + Convert.ToInt32(_params[idx].TempG) + Convert.ToInt32(_params[idx].TempB);
		}
		return base.GetMaxDelay();
	}

	protected override object getBaseEventData(CVStartCFC start, CVCameraNodeProperty property)
	{
		return new LVCameraData(CVImageFlipMode.None, bool.Parse(property.EnableFocus), int.Parse(property.Focus), float.Parse(property.Aperture), int.Parse(property.AvgCount), float.Parse(property.Gain), new float[3]
		{
			Convert.ToSingle(property.TempR),
			Convert.ToSingle(property.TempG),
			Convert.ToSingle(property.TempB)
		}, property.CaliTempName, property.POITempName, property.POIFilterTempName, property.POIReviseTempName, string.Empty);
	}
}
