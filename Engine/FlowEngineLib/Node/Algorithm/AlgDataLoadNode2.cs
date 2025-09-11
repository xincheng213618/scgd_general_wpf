using System.Drawing;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

public class AlgDataLoadNode2 : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(AlgDataLoadNode));

	private string _DataDeviceCode;

	private string _SerialNumber;

	private CVResultType _ResultType;

	private int _DataZIndex;

	private STNodeEditText<string> m_ctrl_dev;

	private STNodeEditText<string> m_ctrl_sn;

	private STNodeEditText<CVResultType> m_ctrl_rt;

	private STNodeEditText<int> m_ctrl_zindex;

	[STNodeProperty("加载设备Code", "加载设备Code", true)]
	public string DataDeviceCode
	{
		get
		{
			return _DataDeviceCode;
		}
		set
		{
			_DataDeviceCode = value;
			m_ctrl_dev.Value = value;
		}
	}

	[STNodeProperty("流水号", "流水号", true)]
	public string SerialNumber
	{
		get
		{
			return _SerialNumber;
		}
		set
		{
			_SerialNumber = value;
			m_ctrl_sn.Value = value;
		}
	}

	[STNodeProperty("结果类型", "结果类型", true)]
	public CVResultType ResultType
	{
		get
		{
			return _ResultType;
		}
		set
		{
			_ResultType = value;
			m_ctrl_rt.Value = value;
		}
	}

	[STNodeProperty("加载ZIndex", "加载ZIndex", true)]
	public int DataZIndex
	{
		get
		{
			return _DataZIndex;
		}
		set
		{
			_DataZIndex = value;
			m_ctrl_zindex.Value = value;
		}
	}

	public AlgDataLoadNode2()
		: base("数据加载2", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "DataLoad";
		_DataDeviceCode = "";
		_SerialNumber = "";
		_DataZIndex = -1;
		base.Height = 150;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_dev = new STNodeEditText<string>();
		m_ctrl_dev.Text = "设备:";
		m_ctrl_dev.DisplayRectangle = new Rectangle(m_custom_item.X, m_custom_item.Y, m_custom_item.Width, m_custom_item.Height);
		m_ctrl_dev.Value = _DataDeviceCode;
		base.Controls.Add(m_ctrl_dev);
		m_ctrl_sn = new STNodeEditText<string>();
		m_ctrl_sn.Text = "流水号:";
		m_ctrl_sn.DisplayRectangle = new Rectangle(m_custom_item.X, m_custom_item.Y + 25, m_custom_item.Width, m_custom_item.Height);
		m_ctrl_sn.Value = _SerialNumber;
		base.Controls.Add(m_ctrl_sn);
		m_ctrl_rt = new STNodeEditText<CVResultType>();
		m_ctrl_rt.Text = "结果类型:";
		m_ctrl_rt.DisplayRectangle = new Rectangle(m_custom_item.X, m_custom_item.Y + 50, m_custom_item.Width, m_custom_item.Height);
		m_ctrl_rt.Value = _ResultType;
		base.Controls.Add(m_ctrl_rt);
		m_ctrl_zindex = new STNodeEditText<int>();
		m_ctrl_zindex.Text = "ZIndex:";
		m_ctrl_zindex.DisplayRectangle = new Rectangle(m_custom_item.X, m_custom_item.Y + 75, m_custom_item.Width, m_custom_item.Height);
		m_ctrl_zindex.Value = _DataZIndex;
		base.Controls.Add(m_ctrl_zindex);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new DataLoadData2(new DataLoadInput(_DataDeviceCode, _SerialNumber, _ResultType, _DataZIndex));
	}
}
