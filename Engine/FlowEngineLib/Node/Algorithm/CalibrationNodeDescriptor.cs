using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

public class CalibrationNodeDescriptor : STNodePropertyDescriptor
{
	private Rectangle m_rect;

	protected override void OnSetItemLocation()
	{
		base.OnSetItemLocation();
		Rectangle rectangleR = base.RectangleR;
		m_rect = new Rectangle(rectangleR.Right - 25, rectangleR.Top + 5, 19, 12);
	}

	protected override object GetValueFromString(string strText)
	{
		return JsonConvert.DeserializeObject<List<CalibrationNodeProperty>>(strText);
	}

	protected override string GetStringFromValue()
	{
		List<CalibrationNodeProperty> list = (List<CalibrationNodeProperty>)GetValue(null);
		if (list != null)
		{
			return JsonConvert.SerializeObject(list);
		}
		return "[]";
	}

	protected override void OnDrawValueRectangle(DrawingTools dt)
	{
		base.OnDrawValueRectangle(dt);
		dt.SolidBrush.Color = Color.Gray;
		dt.Graphics.FillRectangle(dt.SolidBrush, m_rect);
		dt.Graphics.DrawRectangle(Pens.Black, m_rect);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		if (m_rect.Contains(e.Location))
		{
			FormCalibrationProperty formCalibrationProperty = new FormCalibrationProperty();
			formCalibrationProperty.JsonValue = GetStringFromValue();
			if (formCalibrationProperty.ShowDialog() == DialogResult.OK)
			{
				SetValue(formCalibrationProperty.JsonValue);
			}
		}
		else
		{
			base.OnMouseClick(e);
		}
	}
}
