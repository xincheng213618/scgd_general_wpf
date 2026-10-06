#pragma warning disable
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace cvColorVision
{
    public partial class cvCameraCSLib
    {
        // Operation results preserve signed native error codes as int (success 1).
        // Only state/availability predicates use a bool return declaration.
        private const string LIBRARY_CVCAMERA = "cvCamera.dll";

        public static string GetCfgToJson(IntPtr handle, ConfigType eType, bool bDefault)
        {
            StringBuilder builder = new StringBuilder(10240);
            int result = CM_GetCfgToJson(handle, eType, builder, builder.Capacity, bDefault);
            if (result != cvErrorDefine.CV_ERR_SUCCESS) throw NativeError("CM_GetCfgToJson", result);
            return builder.ToString();
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int QHYCCDProcCallBack(int imageType, IntPtr pData, int nW, int nH, int lss, int bpp, int channels, IntPtr usrData);

        private static InvalidOperationException NativeError(string operation, int code)
        {
            string message = string.Empty;
            _ = CM_GetErrorMessage(code, ref message);
            return new InvalidOperationException($"{operation} failed: {code} {message}");
        }

    }
}
