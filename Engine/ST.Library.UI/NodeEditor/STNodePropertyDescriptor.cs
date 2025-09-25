using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace ST.Library.UI.NodeEditor;

public class STNodePropertyDescriptor
{
	private static Type m_t_int = typeof(int);

	private static Type m_t_float = typeof(float);

	private static Type m_t_double = typeof(double);

	private static Type m_t_string = typeof(string);

	private static Type m_t_bool = typeof(bool);

	private StringFormat m_sf;

	public STNode Node { get; internal set; }

	public STNodePropertyGrid Control { get; internal set; }

	public Rectangle Rectangle { get; internal set; }

	public Rectangle RectangleL { get; internal set; }

	public Rectangle RectangleR { get; internal set; }

	public string Name { get; internal set; }

	public string Description { get; internal set; }

	public PropertyInfo PropertyInfo { get; internal set; }

	public bool IsEditEnable { get; internal set; }

	public bool IsReadOnly { get; internal set; }

	public STNodePropertyDescriptor()
	{
		m_sf = new StringFormat();
		m_sf.LineAlignment = StringAlignment.Center;
		m_sf.FormatFlags = StringFormatFlags.NoWrap;
		IsEditEnable = true;
		IsReadOnly = false;
	}

	protected internal virtual void OnSetItemLocation()
	{
	}

	protected internal virtual object GetValueFromString(string strText)
	{
		Type sTNodePropertyDescriptor = PropertyInfo.PropertyType;
		if (sTNodePropertyDescriptor == m_t_int)
		{
			return int.Parse(strText);
		}
		if (sTNodePropertyDescriptor == m_t_float)
		{
			return float.Parse(strText);
		}
		if (sTNodePropertyDescriptor == m_t_double)
		{
			return double.Parse(strText);
		}
		if (sTNodePropertyDescriptor == m_t_string)
		{
			return strText;
		}
		if (sTNodePropertyDescriptor == m_t_bool)
		{
			return bool.Parse(strText);
		}
		if (sTNodePropertyDescriptor.IsEnum)
		{
			return Enum.Parse(sTNodePropertyDescriptor, strText);
		}
		if (sTNodePropertyDescriptor.IsArray)
		{
			Type elementType = sTNodePropertyDescriptor.GetElementType();
			if (elementType == m_t_string)
			{
				return strText.Split(',');
			}
			string[] item = strText.Trim(' ', ',').Split(',');
			if (elementType == m_t_int)
			{
				int[] array = new int[item.Length];
				for (int i = 0; i < item.Length; i++)
				{
					array[i] = int.Parse(item[i].Trim());
				}
				return array;
			}
			if (elementType == m_t_float)
			{
				float[] array2 = new float[item.Length];
				for (int j = 0; j < item.Length; j++)
				{
					array2[j] = float.Parse(item[j].Trim());
				}
				return array2;
			}
			if (elementType == m_t_int)
			{
				double[] array3 = new double[item.Length];
				for (int k = 0; k < item.Length; k++)
				{
					array3[k] = double.Parse(item[k].Trim());
				}
				return array3;
			}
			if (elementType == m_t_int)
			{
				bool[] array4 = new bool[item.Length];
				for (int l = 0; l < item.Length; l++)
				{
					array4[l] = bool.Parse(item[l].Trim());
				}
				return array4;
			}
		}
		throw new InvalidCastException("无法完成[string]到[" + sTNodePropertyDescriptor.FullName + "]的转换 请重载[STNodePropertyDescriptor.GetValueFromString(string)]");
	}

	protected internal virtual string GetStringFromValue()
	{
		object value = PropertyInfo.GetValue(Node, null);
		Type propertyType = PropertyInfo.PropertyType;
		if (value == null)
		{
			return null;
		}
		if (propertyType.IsArray)
		{
			List<string> list = new List<string>();
			foreach (object item in (Array)value)
			{
				list.Add(item.ToString());
			}
			return string.Join(",", list.ToArray());
		}
		return value.ToString();
	}

	protected internal virtual object GetValueFromBytes(byte[] byData)
	{
		if (byData == null)
		{
			return null;
		}
		string strText = Encoding.UTF8.GetString(byData);
		return GetValueFromString(strText);
	}

	protected internal virtual byte[] GetBytesFromValue()
	{
		string stringFromValue = GetStringFromValue();
		if (stringFromValue == null)
		{
			return null;
		}
		return Encoding.UTF8.GetBytes(stringFromValue);
	}

	protected internal virtual object GetValue(object[] index)
	{
		return PropertyInfo.GetValue(Node, index);
	}

	protected internal virtual void SetValue(object value)
	{
		PropertyInfo.SetValue(Node, value, null);
	}

	protected internal virtual void SetValue(string strValue)
	{
		PropertyInfo.SetValue(Node, GetValueFromString(strValue), null);
	}

	protected internal virtual void SetValue(byte[] byData)
	{
		PropertyInfo.SetValue(Node, GetValueFromBytes(byData), null);
	}

	protected internal virtual void SetValue(object value, object[] index)
	{
		PropertyInfo.SetValue(Node, value, index);
	}

	protected internal virtual void SetValue(string strValue, object[] index)
	{
		PropertyInfo.SetValue(Node, GetValueFromString(strValue), index);
	}

	protected internal virtual void SetValue(byte[] byData, object[] index)
	{
		PropertyInfo.SetValue(Node, GetValueFromBytes(byData), index);
	}

	protected internal virtual void OnSetValueError(Exception ex)
	{
		Control.SetErrorMessage(ex.Message);
	}

	protected internal virtual void OnDrawValueRectangle(DrawingTools dt)
	{
		Graphics graphics = dt.Graphics;
		SolidBrush solidBrush = dt.SolidBrush;
		STNodePropertyGrid control = Control;
		solidBrush.Color = control.ItemValueBackColor;
		graphics.FillRectangle(solidBrush, RectangleR);
		Rectangle rectangleR = RectangleR;
		rectangleR.Width--;
		rectangleR.Height--;
		solidBrush.Color = Control.ForeColor;
		graphics.DrawString(GetStringFromValue(), control.Font, solidBrush, RectangleR, m_sf);
		if (PropertyInfo.PropertyType.IsEnum || PropertyInfo.PropertyType == m_t_bool)
		{
			graphics.FillPolygon(Brushes.Gray, new Point[3]
			{
				new Point(rectangleR.Right - 13, rectangleR.Top + rectangleR.Height / 2 - 2),
				new Point(rectangleR.Right - 4, rectangleR.Top + rectangleR.Height / 2 - 2),
				new Point(rectangleR.Right - 9, rectangleR.Top + rectangleR.Height / 2 + 3)
			});
		}
	}

	protected internal virtual void OnMouseEnter(EventArgs e)
	{
	}

	protected internal virtual void OnMouseDown(MouseEventArgs e)
	{
	}

	protected internal virtual void OnMouseMove(MouseEventArgs e)
	{
	}

	protected internal virtual void OnMouseUp(MouseEventArgs e)
	{
	}

	protected internal virtual void OnMouseLeave(EventArgs e)
	{
	}

	protected internal virtual void OnMouseClick(MouseEventArgs e)
	{
		if (IsShowFrm())
		{
			Type propertyType = PropertyInfo.PropertyType;
			if (propertyType == m_t_bool || propertyType.IsEnum)
			{
				new FrmSTNodePropertySelect(this).Show(Control);
			}
			else
			{
				new FrmSTNodePropertyInput(this).Show(Control);
			}
		}
	}

	private bool IsShowFrm()
	{
		if (!IsReadOnly)
		{
			return IsEditEnable;
		}
		return false;
	}

	public void Invalidate()
	{
		Rectangle rectangle = Rectangle;
		rectangle.X -= Control.ScrollOffset;
		Control.Invalidate(rectangle);
	}
}
