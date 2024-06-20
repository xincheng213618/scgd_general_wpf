using ColorVision.Common.Utilities;
using ColorVision.UI;
using cvColorVision;
using log4net;
using System;
using System.Windows;
using System.Windows.Interop;

namespace ColorVision.Engine.Media
{
    public class CoverXYZService : IMainWindowInitialized
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(CoverXYZService));
        public void Initialize()
        {
            IntPtr ConvertXYZhandle = new WindowInteropHelper(Application.Current.GetActiveWindow()).Handle;
            int result = ConvertXYZ.CM_InitXYZ(ConvertXYZhandle);
            logger.Info($"ConvertXYZ.CM_InitXYZ :{result}");
            if (result  ==1)
                ConvertXYZ.Handle = ConvertXYZhandle;
        }
    }
}
