using System;
using System.Drawing;
using ST.Library.UI;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

public class STNodeEditText<T> : STNodeControl
{
	private T _Value;

	private bool _IsLang;

	public T Value
	{
		get
		{
			return _Value;
		}
		set
		{
			_Value = value;
			Invalidate();
		}
	}

	public event EventHandler ValueChanged;

	public STNodeEditText()
	{
		_Value = default(T);
		if (typeof(T).IsEnum)
		{
			_IsLang = true;
		}
	}

	protected virtual void OnValueChanged(EventArgs e)
	{
		if (this.ValueChanged != null)
		{
			this.ValueChanged(this, e);
		}
	}

	protected override void OnPaint(DrawingTools dt)
	{
		base.OnPaint(dt);
		Graphics graphics = dt.Graphics;
		graphics.FillRectangle(Brushes.Gray, base.ClientRectangle);
		m_sf.Alignment = StringAlignment.Near;
		graphics.DrawString(base.Text, base.Font, Brushes.White, base.ClientRectangle, m_sf);
		m_sf.Alignment = StringAlignment.Far;
		if (_IsLang)
		{
			graphics.DrawString(Lang.Get(_Value.ToString()), base.Font, Brushes.White, base.ClientRectangle, m_sf);
		}
		else
		{
			graphics.DrawString(_Value.ToString(), base.Font, Brushes.White, base.ClientRectangle, m_sf);
		}
	}
}
