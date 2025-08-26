using System.Drawing;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

public class AlgDataLoadNode : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(AlgDataLoadNode));

	private string _TempName;

	private STNodeEditText<string> m_ctrl_temp;

	[STNodeProperty("模板", "模板", true)]
	public string TempName
	{
		get
		{
			return _TempName;
		}
		set
		{
			_TempName = value;
			m_ctrl_temp.Value = value;
		}
	}

	public AlgDataLoadNode()
		: base("数据加载", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "DataLoad";
		_TempName = "";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = new STNodeEditText<string>();
		m_ctrl_temp.Text = "模板:";
		m_ctrl_temp.DisplayRectangle = new Rectangle(m_custom_item.X, m_custom_item.Y, m_custom_item.Width, m_custom_item.Height);
		m_ctrl_temp.Value = _TempName;
		base.Controls.Add(m_ctrl_temp);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new DataLoadData(_TempName);
	}
}
