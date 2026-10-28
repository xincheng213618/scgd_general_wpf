using System;
using System.Runtime.InteropServices;

namespace cvColorVision;

public partial class cvCameraCSLib
{
    [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int CM_InitComEx(ref IntPtr handle, FOCUS_COMMUN controller, string port, uint baudRate);

    [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall)]
    public static extern int CM_ClearAlarm(IntPtr handle);

    [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall)]
    public static extern int CM_ShutDown(IntPtr handle);

    [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall)]
    public static extern int CM_SetRunSpeed(IntPtr handle, int speed, int acceleration, int deceleration);

    [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall)]
    public static extern int CM_SetGoHomeSpeed(IntPtr handle, int acceleration, int highSpeed, int lowSpeed);

    [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall)]
    public static extern int CM_SetMotorScope(IntPtr handle, int minPosition, int maxPosition);

    [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall)]
    public static extern int CM_MoveAbsPostion(IntPtr handle, int position, uint timeout);

    [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall)]
    public static extern int CM_GetPosition(IntPtr handle, out int position, uint timeout);

    [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall)]
    public static extern int CM_GoHomeEx(IntPtr handle, int way, uint timeout);

    [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall)]
    public static extern int CM_MoveDiaphragm(IntPtr handle, float position, uint timeout);

    [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall)]
    public static extern int CM_EvaluateFocus(IntPtr cameraHandle, EvaFunc evaluation, out double value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int AutoFocusFrameCallBack(IntPtr userData, int width, int height, int bpp, int channels, IntPtr pixels);

    [DllImport(LIBRARY_CVCAMERA, CallingConvention = CallingConvention.StdCall)]
    public static extern int CM_AutoFocusCallBackEx(IntPtr cameraHandle, AutoFocusFrameCallBack? callback, IntPtr userData);
}
