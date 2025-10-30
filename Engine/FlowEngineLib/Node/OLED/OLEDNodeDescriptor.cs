using System.Drawing;
using System.Windows.Forms;
using FlowEngineLib.Node.Algorithm;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.OLED;

public class OLEDNodeDescriptor : STNodePropertyDescriptor
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
		return JsonConvert.DeserializeObject<PointFloat[]>(strText);
	}

	protected override string GetStringFromValue(bool isLang = false)
	{
		PointFloat[] array = (PointFloat[])GetValue(null);
		if (array != null)
		{
			return JsonConvert.SerializeObject(array);
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
			FormOLEDNodeProperty formOLEDNodeProperty = new FormOLEDNodeProperty();
			formOLEDNodeProperty.JsonValue = GetStringFromValue();
			if (formOLEDNodeProperty.ShowDialog() == DialogResult.OK)
			{
				SetValue(formOLEDNodeProperty.JsonValue);
			}
		}
		else
		{
			base.OnMouseClick(e);
		}
	}
}
