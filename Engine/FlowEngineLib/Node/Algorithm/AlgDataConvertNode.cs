using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

public class AlgDataConvertNode : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(AlgDataLoadNode));

	private CVDataConvertMethodType _MethodType;

	private CVDataConvertInputType _InType;

	private CVDataConvertOutputType _OutType;

	private STNodeEditText<CVDataConvertMethodType> m_ctrl_medType;

	[STNodeProperty("类型", "类型", true)]
	public CVDataConvertMethodType MethodType
	{
		get
		{
			return _MethodType;
		}
		set
		{
			_MethodType = value;
			m_ctrl_medType.Value = value;
		}
	}

	[STNodeProperty("模板", "模板", true)]
	public string TempName
	{
		get
		{
			return _TempName;
		}
		set
		{
			setTempName(value);
		}
	}

	[STNodeProperty("输入", "输入", true)]
	public CVDataConvertInputType InType
	{
		get
		{
			return _InType;
		}
		set
		{
			_InType = value;
		}
	}

	[STNodeProperty("输出", "输出", true)]
	public CVDataConvertOutputType OutType
	{
		get
		{
			return _OutType;
		}
		set
		{
			_OutType = value;
		}
	}

	public AlgDataConvertNode()
		: base("数据转换", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "Math.DataConvert";
		_TempName = "";
		_MethodType = CVDataConvertMethodType.Camera_Motor_VID;
		_InType = CVDataConvertInputType.None;
		_OutType = CVDataConvertOutputType.None;
		base.Height = 100;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_medType = CreateControl(typeof(STNodeEditText<CVDataConvertMethodType>), m_custom_item, "类型:", _MethodType);
		m_custom_item.Y += 25;
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", base.TempDisName);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		DataConvertData dataConvertData = new DataConvertData(_MethodType, _InType, _OutType);
		getPreStepParam(start, dataConvertData);
		dataConvertData.TemplateParam = BuildTemp();
		return dataConvertData;
	}
}
