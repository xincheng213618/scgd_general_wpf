using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Base;

public class LoopNodePropertyDescriptor<T, U> : STNodePropertyDescriptor where U : Form, ILoopFormProperty, new()
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
		return JsonConvert.DeserializeObject<List<T>>(strText);
	}

	protected override string GetStringFromValue()
	{
		List<T> list = (List<T>)GetValue(null);
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
			U val = new U
			{
				JsonValue = GetStringFromValue()
			};
			if (val.ShowDialog() == DialogResult.OK)
			{
				SetValue(val.JsonValue);
			}
		}
		else
		{
			base.OnMouseClick(e);
		}
	}
}
