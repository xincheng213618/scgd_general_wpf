using System;
using System.Collections.Generic;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.Node.Camera;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

internal class LVCameraLoopNode : CVBaseLoopServerNode<CameraNodeProperty>
{
	[STNodeProperty("参数", "Get or set the LVCamera params", false, DescriptorType = typeof(LoopNodePropertyDescriptor<CameraNodeProperty, FormLVCameraProperty>))]
	public List<CameraNodeProperty> Params
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

	public LVCameraLoopNode()
		: base("L/BV相机.For", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
	{
		operatorCode = "GetData";
	}

	protected override int GetMaxDelay()
	{
		if (idx < _params.Count)
		{
			return base.GetMaxDelay() + Convert.ToInt32(_params[idx].Temp);
		}
		return base.GetMaxDelay();
	}

	protected override object getBaseEventData(CVStartCFC start, CameraNodeProperty property)
	{
		return new LVCameraData(CVImageFlipMode.None, bool.Parse(property.EnableFocus), int.Parse(property.Focus), float.Parse(property.Aperture), int.Parse(property.AvgCount), float.Parse(property.Gain), new float[1] { Convert.ToSingle(property.Temp) }, property.CaliTempName, property.POITempName, property.POIFilterTempName, property.POIReviseTempName, string.Empty);
	}
}
