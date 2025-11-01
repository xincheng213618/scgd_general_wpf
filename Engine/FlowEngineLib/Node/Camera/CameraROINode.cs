using System.Collections.Generic;
using System.Drawing;
using System.Dynamic;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Camera;

[STNode("/11 ROI")]
public class CameraROINode : CVBaseServerNode
{
	private int _ROI_X;

	private int _ROI_Y;

	private int _ROI_Width;

	private int _ROI_Height;

	private STNodeEditText<int> m_ctrl_x;

	private STNodeEditText<int> m_ctrl_y;

	private STNodeEditText<int> m_ctrl_width;

	private STNodeEditText<int> m_ctrl_height;

	[STNodeProperty("X", "X", true)]
	public int ROI_X
	{
		get
		{
			return _ROI_X;
		}
		set
		{
			_ROI_X = value;
			m_ctrl_x.Value = value;
		}
	}

	[STNodeProperty("Y", "Y", true)]
	public int ROI_Y
	{
		get
		{
			return _ROI_Y;
		}
		set
		{
			_ROI_Y = value;
			m_ctrl_y.Value = value;
		}
	}

	[STNodeProperty("Width", "Width", true)]
	public int ROI_Width
	{
		get
		{
			return _ROI_Width;
		}
		set
		{
			_ROI_Width = value;
			m_ctrl_width.Value = value;
		}
	}

	[STNodeProperty("Height", "Height", true)]
	public int ROI_Height
	{
		get
		{
			return _ROI_Height;
		}
		set
		{
			_ROI_Height = value;
			m_ctrl_height.Value = value;
		}
	}

	public CameraROINode()
		: base("相机ROI", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
	{
		operatorCode = "SetParam";
		base.Height = 160;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		InitCtrl();
	}

	private void InitCtrl()
	{
		Rectangle custom_item = m_custom_item;
		m_ctrl_x = CreateControl(typeof(STNodeEditText<int>), custom_item, "X:", _ROI_X);
		custom_item.Y += 25;
		m_ctrl_y = CreateControl(typeof(STNodeEditText<int>), custom_item, "Y:", _ROI_Y);
		custom_item.Y += 25;
		m_ctrl_width = CreateControl(typeof(STNodeEditText<int>), custom_item, "Width:", _ROI_Width);
		custom_item.Y += 25;
		m_ctrl_height = CreateControl(typeof(STNodeEditText<int>), custom_item, "Height:", _ROI_Height);
		custom_item.Y += 25;
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		dynamic val = new ExpandoObject();
		val.X = _ROI_X;
		val.Y = _ROI_Y;
		val.Width = _ROI_Width;
		val.Height = _ROI_Height;
		return new SetParamParam(new List<SetParamFuncData>
		{
			new SetParamFuncData
			{
				Name = "CM_SetROI",
				Params = val
			}
		});
	}
}
