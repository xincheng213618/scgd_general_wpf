using ColorVision.Common.MVVM;
using System.Runtime.InteropServices;

namespace StructTestN
{
	public class foucusEdge
	{
		//offy：屏幕与COMS中心的偏差像素；d：屏幕的10-15%；w： COMS的40%-50%；h屏幕的40-50%；nStep：2倍的MAping；nMaxCount：默认20。
		public int offy;
		public int d;
		public int w;
		public int h;
		public int nStep;
		public int nMaxCount;
	}
	public class autoFocusCfg:ViewModelBase
	{
		public string dcf;
		public string focusCOM;
		public int motorNum;
		public int focus_Min;
		public int focus_Max;
		public int focus_Step;
		public float exposure;
		public bool saveImage;

		public foucusEdge EdgeFocus;

		public int StartStep { get; set; } = 500;

		public int EndStep { get; set; } = 30;
	}

	public class logDebug
	{
		private const string LIBRARY_TOOL_LIB = "1TOOL_LIB.dll";

		[DllImport(LIBRARY_TOOL_LIB, EntryPoint = "logCreatEx",
		CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
		public unsafe static extern void logCreatEx();


		[DllImport(LIBRARY_TOOL_LIB, EntryPoint = "logRelease",
			CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
		public unsafe static extern void logRelease();


		[DllImport(LIBRARY_TOOL_LIB, EntryPoint = "logRecord",
		   CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
		public unsafe static extern void logRecord(string eventName);


		[DllImport(LIBRARY_TOOL_LIB, EntryPoint = "logFreshTime",
			CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
		public unsafe static extern void logFreshTime();


		[DllImport(LIBRARY_TOOL_LIB, EntryPoint = "logGetTimeCost",
			CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
		public unsafe static extern void logGetTimeCost(string eventName);
	}
	
}