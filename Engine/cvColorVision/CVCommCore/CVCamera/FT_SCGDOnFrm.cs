using System;

namespace CVCommCore.CVCamera;

public delegate int FT_SCGDOnFrm(E_SCGD_ImgType2 imgType, IntPtr pData, int w, int h, int lss, int bpp, int channels, IntPtr usrData);
