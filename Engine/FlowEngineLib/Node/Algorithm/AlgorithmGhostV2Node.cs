using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_2 Algorithm")]
public class AlgorithmGhostV2Node : CVBaseServerNodeIn2Hub
{
	private string _TempName;

	private int _TempId;

	private string _ImgFileName;

	private int _BufferLen;

	private STNodeEditText<string> m_ctrl_temp;

	[STNodeProperty("参数模板", "参数模板", true)]
	public string TempName
	{
		get
		{
			return _TempName;
		}
		set
		{
			_TempName = value;
			setTempName();
		}
	}

	[STNodeProperty("参数模板ID", "参数模板ID", true)]
	public int TempId
	{
		get
		{
			return _TempId;
		}
		set
		{
			_TempId = value;
			setTempName();
		}
	}

	[STNodeProperty("图像文件", "图像文件", true)]
	public string ImgFileName
	{
		get
		{
			return _ImgFileName;
		}
		set
		{
			_ImgFileName = value;
		}
	}

	[STNodeProperty("缓存大小", "缓存大小", true)]
	public int BufferLen
	{
		get
		{
			return _BufferLen;
		}
		set
		{
			_BufferLen = value;
		}
	}

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TempName}";
	}

	public AlgorithmGhostV2Node()
		: base("ARVR.Ghost算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "Ghost";
		m_in_text = "IN_IMG";
		m_in2_text = "IN_CIE";
		_TempName = "";
		_TempId = -1;
		_BufferLen = 1024;
		base.MaxTime = 15000;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", $"{_TempId}:{_TempName}");
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmGhostInputParam algorithmGhostInputParam = new AlgorithmGhostInputParam(_TempId, _TempName, _BufferLen);
		getPreStepParam(0, algorithmGhostInputParam);
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		getPreStepParam(1, algorithmPreStepParam);
		algorithmGhostInputParam.CIE_MasterId = algorithmPreStepParam.MasterId;
		algorithmGhostInputParam.FileType = GetImageFileType(_ImgFileName);
		algorithmGhostInputParam.ImgFileName = _ImgFileName;
		return algorithmGhostInputParam;
	}
}
