using System;

namespace CVCommCore.CVCamera;

public delegate int AutoFocus_CallBack(IntPtr operate_data, int w, int h, int bpp, int channels, IntPtr pData, int nPos, double evalua);
