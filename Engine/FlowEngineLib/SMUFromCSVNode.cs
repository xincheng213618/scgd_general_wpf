using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/04 源表")]
public class SMUFromCSVNode : SMUBaseNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(SMUFromCSVNode));

	private string _csvFileName;

	[STNodeProperty("电(压/流)源", "电(压/流)源", true)]
	public SourceType Source
	{
		get
		{
			return _source;
		}
		set
		{
			_source = value;
			updateUI();
		}
	}

	[STNodeProperty("通道", "通道", true)]
	public SMUChannelType Channel
	{
		get
		{
			return _channel;
		}
		set
		{
			_channel = value;
			updateUI();
		}
	}

	[STNodeProperty("CsvFileName", "CsvFileName", false, true)]
	public string CsvFileName
	{
		get
		{
			return _csvFileName;
		}
		set
		{
			_csvFileName = value;
			LoadFromCsv(_csvFileName);
			updateUI();
		}
	}

	public SMUFromCSVNode()
		: base("源表[CSV]", "SMU", "SVR.SMU.Default", "DEV.SMU.Default")
	{
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateSMUControl();
		updateUI();
	}

	protected override void updateUI()
	{
		if (IsStarted)
		{
			base.updateUI();
		}
		else if (!string.IsNullOrEmpty(_csvFileName))
		{
			m_ctrl_curValue.Value = Path.GetFileName(_csvFileName);
		}
		else
		{
			m_ctrl_curValue.Value = string.Empty;
		}
	}

	private bool LoadFromCsv(string csvFileName)
	{
		if (!string.IsNullOrEmpty(csvFileName) && File.Exists(csvFileName))
		{
			using FileStream stream = new FileStream(csvFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using StreamReader streamReader = new StreamReader(stream);
			using CsvReader csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture);
			srcValues = csvReader.GetRecords<SMUSrcValueData>().ToList();
			if (srcValues != null && srcValues.Count > 0)
			{
				m_point_num = srcValues.Count;
				m_step_idx = 0;
				m_cur_src_val = srcValues[m_step_idx].SrcValue;
				_limitVal = srcValues[m_step_idx].LimitValue;
				return true;
			}
			if (logger.IsErrorEnabled)
			{
				logger.ErrorFormat("CsvFileName content is empty or has an invalid format.");
			}
			m_point_num = 0;
			m_cur_src_val = 0.0;
			_limitVal = 0f;
			streamReader.Close();
		}
		else
		{
			if (logger.IsErrorEnabled)
			{
				logger.ErrorFormat("CsvFileName is null or not exist.");
			}
			m_point_num = 0;
			m_cur_src_val = 0.0;
			_limitVal = 0f;
		}
		return false;
	}

	protected override double BuildNextSrcValue()
	{
		return srcValues[m_step_idx].SrcValue;
	}

	protected override bool BuildValueData()
	{
		return LoadFromCsv(_csvFileName);
	}
}
