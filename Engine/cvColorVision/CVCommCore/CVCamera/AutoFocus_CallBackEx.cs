using System;

namespace CVCommCore.CVCamera;

public delegate int AutoFocus_CallBackEx(IntPtr operate_data, int w, int h, int bpp, int channels, byte[] pData);
