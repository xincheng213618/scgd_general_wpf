using System.Drawing;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.Node.Algorithm;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/02 相机")]
public class AOILocAndRegPixelsCameraNode : CVBaseServerNode
{
	private ImgSaveBppMode _ImgSaveMode;

	protected string _ImgSaveName;

	protected int _AvgCount;

	protected float _Gain;

	protected float _ExpTime;

	protected bool _IsAutoExp;

	protected string _AutoExpTempName;

	private bool _IsWithND;

	protected string _CaliTempName;

	protected CVImageFlipMode _FlipMode;

	protected string _AlgTempName;

	private CVOLED_Channel _Channel;

	protected string _OutputTempName;

	private const string algParamType = "OLED_FindDotsArrayAndRebuildPixelsMem";

	private STNodeEditText<float> m_ctrl_exp;

	private STNodeEditText<string> m_ctrl_caliTemp;

	private STNodeEditText<string> m_ctrl_outputTemp;

	private STNodeEditText<string> m_ctrl_algTemp;

	[STNodeProperty("保存原图", "保存原图", true)]
	public ImgSaveBppMode ImgSaveMode
	{
		get
		{
			return _ImgSaveMode;
		}
		set
		{
			_ImgSaveMode = value;
			setImgValue();
		}
	}

	[STNodeProperty("图像名称", "图像名称", true)]
	public string ImgSaveName
	{
		get
		{
			return _ImgSaveName;
		}
		set
		{
			_ImgSaveName = value;
		}
	}

	[STNodeProperty("平均次数", "平均次数", true)]
	public int AvgCount
	{
		get
		{
			return _AvgCount;
		}
		set
		{
			_AvgCount = value;
		}
	}

	[STNodeProperty("增益", "增益", true)]
	public float Gain
	{
		get
		{
			return _Gain;
		}
		set
		{
			_Gain = value;
		}
	}

	[STNodeProperty("曝光", "曝光时间", true)]
	public float ExpTime
	{
		get
		{
			return _ExpTime;
		}
		set
		{
			_ExpTime = value;
			m_ctrl_exp.Value = value;
		}
	}

	[STNodeProperty("自动曝光", "自动曝光", true)]
	public bool IsAutoExp
	{
		get
		{
			return _IsAutoExp;
		}
		set
		{
			_IsAutoExp = value;
		}
	}

	[STNodeProperty("曝光模板", "自动曝光模板", true)]
	public string AutoExpTempName
	{
		get
		{
			return _AutoExpTempName;
		}
		set
		{
			_AutoExpTempName = value;
		}
	}

	[STNodeProperty("启用ND", "启用ND滤轮(自动曝光)", true)]
	public bool IsWithND
	{
		get
		{
			return _IsWithND;
		}
		set
		{
			_IsWithND = value;
		}
	}

	[STNodeProperty("校正模板", "校正模板", true)]
	public string CaliTempName
	{
		get
		{
			return _CaliTempName;
		}
		set
		{
			_CaliTempName = value;
			m_ctrl_caliTemp.Value = value;
		}
	}

	[STNodeProperty("图像翻转", "图像翻转", true)]
	public CVImageFlipMode FlipMode
	{
		get
		{
			return _FlipMode;
		}
		set
		{
			_FlipMode = value;
		}
	}

	[STNodeProperty("提取模板", "提取模板", true)]
	public string AlgTempName
	{
		get
		{
			return _AlgTempName;
		}
		set
		{
			_AlgTempName = value;
			setAOIValue();
		}
	}

	[STNodeProperty("通道", "通道", true)]
	public CVOLED_Channel Channel
	{
		get
		{
			return _Channel;
		}
		set
		{
			_Channel = value;
		}
	}

	[STNodeProperty("输出模板", "输出模板", true)]
	public string OutputTempName
	{
		get
		{
			return _OutputTempName;
		}
		set
		{
			_OutputTempName = value;
			setOutputTemp();
		}
	}

	public AOILocAndRegPixelsCameraNode()
		: base("AOILocAndRegPixels", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
	{
		operatorCode = "GetDataAndAlgorithm";
		_FlipMode = CVImageFlipMode.None;
		_ImgSaveMode = ImgSaveBppMode.Bit16;
		_Channel = CVOLED_Channel.GREEN;
		_ExpTime = 100f;
		_Gain = 10f;
		_IsWithND = false;
		_IsAutoExp = false;
		_CaliTempName = "";
		_AutoExpTempName = "";
		_OutputTempName = "";
		_ImgSaveName = "";
		_AlgTempName = "";
		_AvgCount = 1;
		base.Height += 75;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_exp = CreateControl(typeof(STNodeEditText<float>), m_custom_item, "曝光:", _ExpTime);
		Rectangle custom_item = m_custom_item;
		custom_item.Y += 25;
		m_ctrl_caliTemp = CreateControl(typeof(STNodeEditText<string>), custom_item, "校正模板:", _CaliTempName);
		custom_item.Y += 25;
		m_ctrl_algTemp = CreateControl(typeof(STNodeEditText<string>), custom_item, "提取模板:", _AlgTempName);
		custom_item.Y += 25;
		m_ctrl_outputTemp = CreateControl(typeof(STNodeEditText<string>), custom_item, "输出模板:", _OutputTempName);
	}

	private void setImgValue()
	{
	}

	private void setOutputTemp()
	{
		m_ctrl_outputTemp.Value = _OutputTempName;
	}

	private void setAOIValue()
	{
		m_ctrl_algTemp.Value = GetAlgTempDis();
	}

	private string GetAlgTempDis()
	{
		return $"{_AlgTempName}";
	}

	private string GetImgTempDis()
	{
		return $"{_ImgSaveMode.ToString()}:{_FlipMode.ToString()}";
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		getPreStepParam(1, algorithmPreStepParam);
		return new CVAOIBVRegCameraParam(_ImgSaveName, _FlipMode, _AvgCount, _Gain, new float[1] { _ExpTime }, _IsWithND, _IsAutoExp, _AutoExpTempName, _CaliTempName, "OLED_FindDotsArrayAndRebuildPixelsMem", _AlgTempName, _Channel, _OutputTempName, algorithmPreStepParam.MasterId, (int)_ImgSaveMode);
	}

	protected override int GetMaxDelay()
	{
		return base.GetMaxDelay() + (int)_ExpTime;
	}
}
