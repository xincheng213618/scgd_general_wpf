using System;
using log4net;

namespace FlowEngineLib;

public static class LogHelper
{
	public static readonly ILog loginfo = LogManager.GetLogger("loginfo");

	public static readonly ILog logerror = LogManager.GetLogger("logerror");

	public static ILog GetInfoLog()
	{
		return loginfo;
	}

	public static void WriteLog(string info)
	{
		if (loginfo.IsInfoEnabled)
		{
			loginfo.Info((object)info);
		}
	}

	public static void ErrorLog(string info, Exception ex)
	{
		if (!string.IsNullOrEmpty(info) && ex == null)
		{
			logerror.ErrorFormat("【附加信息】 : {0}<br>", new object[1] { info });
		}
		else if (!string.IsNullOrEmpty(info) && ex != null)
		{
			string text = BeautyErrorMsg(ex);
			logerror.ErrorFormat("【附加信息】 : {0}<br>{1}", new object[2] { info, text });
		}
		else if (string.IsNullOrEmpty(info) && ex != null)
		{
			string text2 = BeautyErrorMsg(ex);
			logerror.Error((object)text2);
		}
	}

	private static string BeautyErrorMsg(Exception ex)
	{
		return string.Format("【异常类型】：{0} <br>【异常信息】：{1} <br>【堆栈调用】：{2}", new object[3]
		{
			ex.GetType().Name,
			ex.Message,
			ex.StackTrace
		}).Replace("\r\n", "<br>").Replace("位置", "<strong style=\"color:red\">位置</strong>");
	}
}
