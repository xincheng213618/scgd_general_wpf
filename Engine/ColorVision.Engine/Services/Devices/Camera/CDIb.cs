using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;

namespace PhotoShow
{

    public class CDIb : ContextBoundObject
    {
        private int m_iWndWidth;
        private int m_iWndHeight;
        private int m_iShowWidth;
        private int m_iShowHeight;
        private double m_dStrIMG_X = 0;
        private double m_dStrIMG_Y = 0;
        private int m_nStrWnd_X = 0;
        private int m_nStrWnd_Y = 0;
        private float m_fRate = 1.0f;
        public float m_dZoomRate = 1.0f;

        public int m_iImgWid;
        public int m_iImgHei;
        public int m_nBpp;
        public int m_nChannels;
        public Bitmap SrcBitmap { get; set; }
        private byte[] Imgdata = null;

        public CDIb()
        {
            m_fRate = 1.0f;
            m_dZoomRate = 1.0f;
            m_iWndWidth = 0;
            m_iWndHeight = 0;
            m_iShowWidth = 0;
            m_iShowHeight = 0;
            m_iImgWid = 0;
            m_iImgHei = 0;
            SrcBitmap = null;
        }
        ~CDIb()
        {
            Imgdata = null;
            SrcBitmap = null;
        }
        public void Initial(int iWndWid, int iWndHei)
        {
            m_iWndWidth = iWndWid;
            m_iWndHeight = iWndHei;
        }

        public void ReSetInitial(int iWndWid, int iWndHei)
        {
            m_iWndWidth = iWndWid;
            m_iWndHeight = iWndHei;

            if (SrcBitmap != null)
            {
                float ratW = (float)m_iImgWid / (float)m_iWndWidth;
                float ratH = (float)m_iImgHei / (float)m_iWndHeight;

                if (ratH < ratW)
                {
                    m_fRate = ratW;
                }
                else
                {
                    m_fRate = ratH;
                }

                m_iShowWidth = (int)((float)m_iImgWid / m_fRate + 0.5);
                m_iShowHeight = (int)((float)m_iImgHei / m_fRate + 0.5);
            }
        }
        public int GetBpp()
        {
            return m_nBpp;
        }

        public int GetShowWidth()
        {
            return m_iShowWidth;
        }

        public int GetShowHeight()
        {
            return m_iShowHeight;
        }
        public double GetScale()
        {
            return m_dZoomRate / m_fRate;
        }
        public int GetStartX()
        {
            return (int)(m_nStrWnd_X - m_dStrIMG_X);
        }
        public int GetStartY()
        {
            return (int)(m_nStrWnd_Y - m_dStrIMG_Y);
        }
        public bool GetImgPoint(int wnd_x, int wnd_y, ref int img_x, ref int img_y)
        {
            if (SrcBitmap == null)
            {
                return false;
            }

            if(wnd_x < 0 || wnd_y < 0  ||
               wnd_x < m_nStrWnd_X || wnd_y < m_nStrWnd_Y ||
               wnd_x >= m_iWndWidth - m_nStrWnd_X || wnd_y >= m_iWndHeight - m_nStrWnd_Y)
            {
                return false;
            }

            img_x = (int)((wnd_x - GetStartX()) / GetScale());
            img_y = (int)((wnd_y - GetStartY()) / GetScale());

            if (img_x < 0)
                img_x = 0;
            if (img_x >= m_iImgWid)
                img_x = m_iImgWid - 1;
            if (img_y < 0)
                img_y = 0;
            if (img_y >= m_iImgHei)
                img_y = m_iImgHei - 1;

            return true;
        }
        public byte[] GetBuffer()
        {
            return Imgdata;
        }
        public void ClearBuffer()
        {
            Imgdata = null;

            if(SrcBitmap != null)
            {
                SrcBitmap.Dispose();
                SrcBitmap = null;
            }
        }
        public void InputImg(byte[] pImg, int nBPp, int nChannels, int iIMGWid, int IIMGHei)
        {
            m_iImgWid = iIMGWid;
            m_iImgHei = IIMGHei;

            float ratW = (float)m_iImgWid / (float)m_iWndWidth;
            float ratH = (float)m_iImgHei / (float)m_iWndHeight;

            if (ratH < ratW)
            {
                m_fRate = ratW;
            }
            else
            {
                m_fRate = ratH;
            }

            m_iShowWidth = (int)((float)m_iImgWid / m_fRate + 0.5);
            m_iShowHeight = (int)((float)m_iImgHei / m_fRate + 0.5);

            if (Imgdata == null || Imgdata.Length != pImg.Length)
            {
                Imgdata = null;
                Imgdata = new byte[pImg.Length];
            }

            m_nBpp = nBPp;
            m_nChannels = nChannels;

            pImg.CopyTo(Imgdata, 0);

            ToScrBitmap(iIMGWid, IIMGHei, Imgdata, nBPp, nChannels, SrcBitmap);
        }

        public void InputImg(IntPtr pData, int nBPp, int nChannels, int iIMGWid, int IIMGHei)
        {
            m_iImgWid = iIMGWid;
            m_iImgHei = IIMGHei;

            float ratW = (float)m_iImgWid / (float)m_iWndWidth;
            float ratH = (float)m_iImgHei / (float)m_iWndHeight;

            if (ratH < ratW)
            {
                m_fRate = ratW;
            }
            else
            {
                m_fRate = ratH;
            }

            m_iShowWidth = (int)((float)m_iImgWid / m_fRate + 0.5);
            m_iShowHeight = (int)((float)m_iImgHei / m_fRate + 0.5);

            int nSizeMen = nBPp / 8;
            nSizeMen *= iIMGWid * IIMGHei * nChannels;

            Imgdata = null;
            Imgdata = new byte[nSizeMen];
            m_nBpp = nBPp;
            m_nChannels = nChannels;

            Marshal.Copy(pData, Imgdata, 0, Imgdata.Length);

            ToScrBitmap(iIMGWid, IIMGHei, Imgdata, nBPp, nChannels, SrcBitmap);
        }

        public void InputImg(Bitmap bitmap)
        {
            m_iImgWid = bitmap.Width;
            m_iImgHei = bitmap.Height;
            SrcBitmap = new System.Drawing.Bitmap(bitmap);

            float ratW = (float)m_iImgWid / (float)m_iWndWidth;
            float ratH = (float)m_iImgHei / (float)m_iWndHeight;

            if (ratH < ratW)
            {
                m_fRate = ratW;
            }
            else
            {
                m_fRate = ratH;
            }

            m_iShowWidth = (int)((float)m_iImgWid / m_fRate + 0.5);
            m_iShowHeight = (int)((float)m_iImgHei / m_fRate + 0.5);
        }

        public bool GetPixelWnd(int show_x, int show_y, ref int i0, ref int i1, ref int i2)
        {
            bool bRet = false;

            int x = 0 , y = 0;

            if(GetImgPoint(show_x, show_y, ref x, ref y))
            {
                if (m_nBpp == 8)
                {
                    if (m_nChannels == 1)
                    {
                        i0 = Imgdata[m_iImgWid * y + x];
                        i1 = Imgdata[m_iImgWid * y + x];
                        i2 = Imgdata[m_iImgWid * y + x];
                    }
                    else
                    {
                        i0 = Imgdata[m_iImgWid * y * 3 + x * 3 + 0];
                        i1 = Imgdata[m_iImgWid * y * 3 + x * 3 + 1];
                        i2 = Imgdata[m_iImgWid * y * 3 + x * 3 + 2];
                    }

                    bRet = true;
                }
                else if (m_nBpp == 16)
                {
                    if (m_nChannels == 1)
                    {
                        i0 = BitConverter.ToUInt16(Imgdata, m_iImgWid * y * 2 + x * 2);
                        i1 = BitConverter.ToUInt16(Imgdata, m_iImgWid * y * 2 + x * 2);
                        i2 = BitConverter.ToUInt16(Imgdata, m_iImgWid * y * 2 + x * 2);
                    }
                    else
                    {
                        i0 = BitConverter.ToUInt16(Imgdata, m_iImgWid * y * 6 + x * 6 + 0);
                        i1 = BitConverter.ToUInt16(Imgdata, m_iImgWid * y * 6 + x * 6 + 2);
                        i2 = BitConverter.ToUInt16(Imgdata, m_iImgWid * y * 6 + x * 6 + 4);
                    }

                    bRet = true;
                }
            }

            return bRet;
        }
        public bool GetPixelWnd(int show_x, int show_y, ref float i0, ref float i1, ref float i2)
        {
            bool bRet = false;

            int x = 0, y = 0;

            if (GetImgPoint(show_x, show_y, ref x, ref y))
            {
                if (m_nBpp == 32)
                {
                    if (m_nChannels == 1)
                    {
                        i0 = BitConverter.ToSingle(Imgdata, m_iImgWid * y * 12 + x * 12);
                        i1 = BitConverter.ToSingle(Imgdata, m_iImgWid * y * 12 + x * 12);
                        i2 = BitConverter.ToSingle(Imgdata, m_iImgWid * y * 12 + x * 12);
                    }
                    else
                    {
                        i0 = BitConverter.ToSingle(Imgdata, m_iImgWid * y * 12 + x * 12 + 0);
                        i1 = BitConverter.ToSingle(Imgdata, m_iImgWid * y * 12 + x * 12 + 4);
                        i2 = BitConverter.ToSingle(Imgdata, m_iImgWid * y * 12 + x * 12 + 8);
                    }

                    bRet = true;
                }
            }

            return bRet;
        }

        public bool GetPixelWndRGB(int show_x, int show_y, ref int i0, ref int i1, ref int i2)
        {
            bool bRet = false;

            int x = 0, y = 0;

            if (GetImgPoint(show_x, show_y, ref x, ref y))
            {
                if(SrcBitmap == null)
                {
                    return bRet;
                }

                if (x >= 0 && x < SrcBitmap.Width &&
                    y >= 0 && y < SrcBitmap.Height)
                {
                    Color cl = SrcBitmap.GetPixel(x, y);

                    i0 = cl.R;
                    i1 = cl.G;
                    i2 = cl.B;
                }

                bRet = true;
            }

            return bRet;
        }
        public bool GetPixel(int Img_x, int img_y, ref int i0, ref int i1, ref int i2)
        {
            bool bRet = false;

            int x = Img_x, y = img_y;

            if (SrcBitmap == null)
            {
                return bRet;
            }

            if (x >= 0 && x < SrcBitmap.Width &&
                y >= 0 && y < SrcBitmap.Height)
            {
                if (m_nBpp == 8)
                {
                    if (m_nChannels == 1)
                    {
                        i0 = Imgdata[m_iImgWid * y + x];
                        i1 = Imgdata[m_iImgWid * y + x];
                        i2 = Imgdata[m_iImgWid * y + x];
                    }
                    else
                    {
                        i0 = Imgdata[m_iImgWid * y * 3 + x * 3 + 0];
                        i1 = Imgdata[m_iImgWid * y * 3 + x * 3 + 1];
                        i2 = Imgdata[m_iImgWid * y * 3 + x * 3 + 2];
                    }

                    bRet = true;
                }
                else if (m_nBpp == 16)
                {
                    if (m_nChannels == 1)
                    {
                        i0 = BitConverter.ToUInt16(Imgdata, m_iImgWid * y * 2 + x * 2);
                        i1 = BitConverter.ToUInt16(Imgdata, m_iImgWid * y * 2 + x * 2);
                        i2 = BitConverter.ToUInt16(Imgdata, m_iImgWid * y * 2 + x * 2);
                    }
                    else
                    {
                        i0 = BitConverter.ToUInt16(Imgdata, m_iImgWid * y * 6 + x * 6 + 0);
                        i1 = BitConverter.ToUInt16(Imgdata, m_iImgWid * y * 6 + x * 6 + 2);
                        i2 = BitConverter.ToUInt16(Imgdata, m_iImgWid * y * 6 + x * 6 + 4);
                    }

                    bRet = true;
                }
            }

            return bRet;
        }
        public bool GetPixel(int Img_x, int Img_y, ref float i0, ref float i1, ref float i2)
        {
            bool bRet = false;

            int x = Img_x, y = Img_y;

            if (SrcBitmap == null)
            {
                return bRet;
            }

            if (x >= 0 && x < SrcBitmap.Width &&
                y >= 0 && y < SrcBitmap.Height)
            {
                if (m_nBpp == 32)
                {
                    if (m_nChannels == 1)
                    {
                        i0 = BitConverter.ToSingle(Imgdata, m_iImgWid * y * 12 + x * 12);
                        i1 = BitConverter.ToSingle(Imgdata, m_iImgWid * y * 12 + x * 12);
                        i2 = BitConverter.ToSingle(Imgdata, m_iImgWid * y * 12 + x * 12);
                    }
                    else
                    {
                        i0 = BitConverter.ToSingle(Imgdata, m_iImgWid * y * 12 + x * 12 + 0);
                        i1 = BitConverter.ToSingle(Imgdata, m_iImgWid * y * 12 + x * 12 + 4);
                        i2 = BitConverter.ToSingle(Imgdata, m_iImgWid * y * 12 + x * 12 + 8);
                    }

                    bRet = true;
                }
            }

            return bRet;
        }

        public bool GetPixelRGB(int Img_x, int Img_y, ref int i0, ref int i1, ref int i2)
        {
            bool bRet = false;

            int x = Img_x, y = Img_y;

            if (SrcBitmap == null)
            {
                return bRet;
            }

            if (x >= 0 && x < SrcBitmap.Width &&
                y >= 0 && y < SrcBitmap.Height)
            {
                Color cl = SrcBitmap.GetPixel(x, y);

                i0 = cl.R;
                i1 = cl.G;
                i2 = cl.B;
            }

            bRet = true;

            return bRet;
        }

        public void MoveImage(int show_x, int show_y)
        {
            if (SrcBitmap == null)
                return;

            m_dStrIMG_X = m_dStrIMG_X + show_x;
            m_dStrIMG_Y = m_dStrIMG_Y + show_y;

            if (m_dStrIMG_X < 0)
            {
                m_dStrIMG_X = 0;
            }

            if (m_dStrIMG_Y < 0)
            {
                m_dStrIMG_Y = 0;
            }

            int nZoomIMGSize_X = (int)(m_dZoomRate * m_iShowWidth);
            int nZoomIMGSize_Y = (int)(m_dZoomRate * m_iShowHeight);

            int nShowWndWid = (int)(m_iShowWidth * m_dZoomRate);
            int nShowWndHei = (int)(m_iShowHeight * m_dZoomRate);

            if (nShowWndWid > m_iWndWidth)
            {
                nShowWndWid = m_iWndWidth;
            }

            if (nShowWndHei > m_iWndHeight)
            {
                nShowWndHei = m_iWndHeight;
            }

            if (m_dStrIMG_X > nZoomIMGSize_X - nShowWndWid)
            {
                m_dStrIMG_X = nZoomIMGSize_X - nShowWndWid;
            }

            if (m_dStrIMG_Y > nZoomIMGSize_Y - nShowWndHei)
            {
                m_dStrIMG_Y = nZoomIMGSize_Y - nShowWndHei;
            }
        }
        public bool ZoomRectImage(Rect rect)
        {
            if (SrcBitmap == null)
                return false;

            int nCT_X = (int)(rect.X + rect.Width / 2);
            int nCT_Y = (int)(rect.Y + rect.Height / 2);

            float dZoomRateX = (float)(rect.Width / m_iShowWidth);
            float dZoomRateY = (float)(rect.Height / m_iShowHeight);

            float dZoomRate = dZoomRateX < dZoomRateY ? dZoomRateX : dZoomRateY;

            return ZoomAbsImage(nCT_X, nCT_Y, dZoomRate);
        }
        public bool ZoomAbsImage(int show_x, int show_y, float dZoomRate)
        {
            if (SrcBitmap == null)
                return false;

            double dimgX = (m_dStrIMG_X + show_x) / m_dZoomRate;
            double dimgY = (m_dStrIMG_Y + show_y) / m_dZoomRate;

            if (dZoomRate > 10 * m_fRate)
                return false;
            if (dZoomRate < 1)
            {
                m_dZoomRate = 1.0f;
            }
            else
            {
                m_dZoomRate = dZoomRate;
            }

            dimgX = m_dZoomRate * dimgX;
            dimgY = m_dZoomRate * dimgY;

            m_dStrIMG_X = dimgX - show_x;
            m_dStrIMG_Y = dimgY - show_y;

            if (m_dStrIMG_X < 0)
            {
                m_dStrIMG_X = 0;
            }

            if (m_dStrIMG_Y < 0)
            {
                m_dStrIMG_Y = 0;
            }

            int nZoomIMGSize_X = (int)(m_dZoomRate * m_iShowWidth);
            int nZoomIMGSize_Y = (int)(m_dZoomRate * m_iShowHeight);

            int nShowWndWid = (int)(m_iShowWidth * m_dZoomRate);
            int nShowWndHei = (int)(m_iShowHeight * m_dZoomRate);

            if (nShowWndWid > m_iWndWidth)
            {
                nShowWndWid = m_iWndWidth;
            }

            if (nShowWndHei > m_iWndHeight)
            {
                nShowWndHei = m_iWndHeight;
            }

            if (m_dStrIMG_X > nZoomIMGSize_X - nShowWndWid)
            {
                m_dStrIMG_X = nZoomIMGSize_X - nShowWndWid;
            }

            if (m_dStrIMG_Y > nZoomIMGSize_Y - nShowWndHei)
            {
                m_dStrIMG_Y = nZoomIMGSize_Y - nShowWndHei;
            }

            return true;
        }

        public bool ZoomImage(int show_x, int show_y, float dZoomRate)
        {
            if (SrcBitmap == null)
                return false;

            double dimgX = (m_dStrIMG_X + show_x) / m_dZoomRate;
            double dimgY = (m_dStrIMG_Y + show_y) / m_dZoomRate;

            if (m_dZoomRate * (1 + dZoomRate) > 10 * m_fRate)
                return false;
            if (m_dZoomRate * (1 + dZoomRate) < 1)
            {
                m_dZoomRate = 1.0f;
            }
            else
            {
                m_dZoomRate *= (1 + dZoomRate);
            }

            dimgX = m_dZoomRate * dimgX;
            dimgY = m_dZoomRate * dimgY;

            m_dStrIMG_X = dimgX - show_x;
            m_dStrIMG_Y = dimgY - show_y;

            if (m_dStrIMG_X < 0)
            {
                m_dStrIMG_X = 0;
            }

            if (m_dStrIMG_Y < 0)
            {
                m_dStrIMG_Y = 0;
            }

            int nZoomIMGSize_X = (int)(m_dZoomRate * m_iShowWidth);
            int nZoomIMGSize_Y = (int)(m_dZoomRate * m_iShowHeight);

            int nShowWndWid = (int)(m_iShowWidth * m_dZoomRate);
            int nShowWndHei = (int)(m_iShowHeight * m_dZoomRate);

            if (nShowWndWid > m_iWndWidth)
            {
                nShowWndWid = m_iWndWidth;
            }

            if (nShowWndHei > m_iWndHeight)
            {
                nShowWndHei = m_iWndHeight;
            }

            if(m_dStrIMG_X > nZoomIMGSize_X - nShowWndWid)
            {
                m_dStrIMG_X = nZoomIMGSize_X - nShowWndWid;
            }

            if (m_dStrIMG_Y > nZoomIMGSize_Y - nShowWndHei)
            {
                m_dStrIMG_Y = nZoomIMGSize_Y - nShowWndHei;
            }

            return true;
        }

        public void Draw(Graphics g)
        {
            int nShowWid = (int)(m_iShowWidth * m_dZoomRate);
            int nShowHei = (int)(m_iShowHeight * m_dZoomRate);

            m_nStrWnd_X = (m_iWndWidth - nShowWid) / 2;
            m_nStrWnd_Y = (m_iWndHeight - nShowHei) / 2;

            if (nShowWid > m_iWndWidth)
            {
                m_nStrWnd_X = 0;
                nShowWid = m_iWndWidth;
            }

            if (nShowHei > m_iWndHeight)
            {
                m_nStrWnd_Y = 0;
                nShowHei = m_iWndHeight;
            }

            m_nIMGRealWid = (int)(SrcBitmap.Width * nShowWid / (m_iShowWidth * m_dZoomRate));
            m_nIMGRealHei = (int)(SrcBitmap.Height * nShowHei / (m_iShowHeight * m_dZoomRate));

            int x;
            int y;

            x = (int)((m_dStrIMG_X) * m_fRate / m_dZoomRate);
            y = (int)((m_dStrIMG_Y) * m_fRate / m_dZoomRate);

            g.DrawImage(SrcBitmap, new Rectangle(m_nStrWnd_X, m_nStrWnd_Y, nShowWid, nShowHei)
                    , x, y, m_nIMGRealWid, m_nIMGRealHei, GraphicsUnit.Pixel);
        }

        int m_nIMGRealHei;
        int m_nIMGRealWid;

        public void DrawIMG(Graphics g)
        {
            int nShowWid = (int)(m_iShowWidth * m_dZoomRate);
            int nShowHei = (int)(m_iShowHeight * m_dZoomRate);

            m_nStrWnd_X = (m_iWndWidth - nShowWid) / 2;
            m_nStrWnd_Y = (m_iWndHeight - nShowHei) / 2;

            if (nShowWid > m_iWndWidth)
            {
                m_nStrWnd_X = 0;
                nShowWid = m_iWndWidth;
            }

            if (nShowHei > m_iWndHeight)
            {
                m_nStrWnd_Y = 0;
                nShowHei = m_iWndHeight;
            }


            int x;
            int y;

            int nShowIMGWid = (int)(m_iShowWidth * m_dZoomRate);
            int nShowIMGHei = (int)(m_iShowHeight * m_dZoomRate);
            x = (nShowIMGWid - nShowWid) / 2;
            y = (nShowIMGHei - nShowHei) / 2;

            g.DrawImage(SrcBitmap, new Rectangle(m_nStrWnd_X, m_nStrWnd_Y, nShowWid, nShowHei)
                    , x, y, nShowWid, nShowHei, GraphicsUnit.Pixel);
        }

        #region ToBitmap

        byte[] pDstdata = null;
        ushort[] punSrcdata = null;
        float[] pfSrcdata = null;

        private bool ToScrBitmap(int nWid, int nHei, byte[] pdata, int nBPp, int nChannels, Bitmap dst)
        {
            if (nWid == 0 || nHei == 0 || pdata == null || nChannels == 0)
                return false;

            if (nBPp == 8)
            {
                SrcBitmap = ToBitmap(nWid, nHei, pdata, 8, nChannels);
            }

            if (nBPp == 16)
            {
                if (pDstdata == null || pDstdata.Length != (nWid * nHei * nChannels))
                {
                    pfSrcdata = null;
                    pDstdata = new byte[nWid * nHei * nChannels];
                    //punSrcdata = new ushort[nWid * nHei * nChannels];
                }

               // Buffer.BlockCopy(pdata, 0, punSrcdata, 0, (int)(nWid * nHei * nChannels) * 2);

                uint nWidthStep = (uint)(((nWid * nChannels * 8 / 8) + 3) & ~3);

                for (int h = 0; h < nHei; h++)
                {
                    for (int w = 0; w < nWid * nChannels; w++)
                    {
                        int nPos = h * nWid * nChannels + w;
                        pDstdata[nPos] = (byte)(pdata[nPos * 2 + 1]);
                    }
                }

                SrcBitmap = ToBitmap(nWid, nHei, pDstdata, 8, nChannels);
            }

            if (nBPp == 32)
            {
                if (pDstdata == null || pDstdata.Length != (nWid * nHei * nChannels))
                {
                    punSrcdata = null;
                    pDstdata = new byte[nWid * nHei * nChannels];
                    pfSrcdata = new float[nWid * nHei * nChannels];
                }

                Buffer.BlockCopy(pdata, 0, pfSrcdata, 0, (int)(nWid * nHei * nChannels) * 4);

                uint nWidthStep = (uint)(((nWid * nChannels * 8 / 8) + 3) & ~3);

                for (int h = 0; h < nHei; h++)
                {
                    for (int w = 0; w < nWidthStep; w++)
                    {
                        int nValue = (int)(pfSrcdata[h * nWidthStep + w] * 255);

                        if (nValue > 255)
                        {
                            pDstdata[h * nWidthStep + w] = 255;
                        }
                        else if (nValue < 0)
                        {
                            pDstdata[h * nWidthStep + w] = 0;
                        }
                        else
                        {
                            pDstdata[h * nWidthStep + w] = (byte)nValue;
                        }
                    }
                }

                SrcBitmap = ToBitmap(nWid, nHei, pDstdata, 8, nChannels);
            }

            return false;
        }

        public static Bitmap ToBitmap(int nWid, int nHei, byte[] pdata, int nBPp, int nChannels)
        {
            if (pdata == null)
            {
                throw new ArgumentNullException(nameof(pdata));
            }

            PixelFormat pf;
            switch (nChannels)
            {
                case 1:
                    pf = PixelFormat.Format8bppIndexed; break;
                case 3:
                    pf = PixelFormat.Format24bppRgb; break;
                case 4:
                    pf = PixelFormat.Format32bppArgb; break;
                default:
                    throw new ArgumentException("Number of channels must be 1, 3 or 4.", nameof(nChannels));
            }
            return ToBitmap(nWid, nHei, pdata, nBPp, nChannels, pf);
        }

        public static Bitmap ToBitmap(int nWid, int nHei, byte[] pdata, int nBPp, int nChannels, PixelFormat pf)
        {
            if (pdata == null)
                throw new ArgumentNullException(nameof(pdata));

            Bitmap bitmap = new Bitmap(nWid, nHei, pf);
            ToBitmap(nWid, nHei, pdata, nBPp, nChannels, bitmap);
            return bitmap;
        }
        public static unsafe void ToBitmap(int nWid, int nHei, byte[] pdata, int nBPp, int nChannels, Bitmap dst)
        {
            if (pdata == null)
                throw new ArgumentNullException(nameof(pdata));
            if (dst == null)
                throw new ArgumentNullException(nameof(dst));
            if (nBPp != 8)
                throw new ArgumentException("Depth of the image must be CV_8U");
            if (nWid != dst.Width || nHei != dst.Height)
                throw new ArgumentException("");

            PixelFormat pf = dst.PixelFormat;

            if (pf == PixelFormat.Format8bppIndexed)
            {
                ColorPalette plt = dst.Palette;
                for (int x = 0; x < 256; x++)
                {
                    plt.Entries[x] = Color.FromArgb(x, x, x);
                }
                dst.Palette = plt;
            }

            int w = nWid;
            int h = nHei;
            Rectangle rect = new Rectangle(0, 0, w, h);
            BitmapData bd = null;

            try
            {
                bd = dst.LockBits(rect, ImageLockMode.WriteOnly, pf);

                GCHandle hdata = GCHandle.Alloc(pdata, GCHandleType.Pinned);
                IntPtr srcData = hdata.AddrOfPinnedObject();
                byte* pSrc = (byte*)(srcData.ToPointer());
                byte* pDst = (byte*)(bd.Scan0.ToPointer());
                int ch = nChannels;
                int srcStep = nWid * ch;
                int dstStep = ((nWid * ch) + 3) / 4 * 4;
                int stride = bd.Stride;

                switch (pf)
                {
                    case PixelFormat.Format1bppIndexed:
                        {
                            int x = 0;
                            byte b = 0;
                            for (int y = 0; y < h; y++)
                            {
                                for (int bytePos = 0; bytePos < stride; bytePos++)
                                {
                                    if (x < w)
                                    {
                                        for (int i = 0; i < 8; i++)
                                        {
                                            var mask = (byte)(0x80 >> i);
                                            if (x < w && pSrc[srcStep * y + x] == 0)
                                                b &= (byte)(mask ^ 0xff);
                                            else
                                                b |= mask;

                                            x++;
                                        }
                                        pDst[bytePos] = b;
                                    }
                                }
                                x = 0;
                                pDst += stride;
                            }
                            break;
                        }

                    case PixelFormat.Format8bppIndexed:
                    case PixelFormat.Format24bppRgb:
                    case PixelFormat.Format32bppArgb:

                        for (int y = 0; y < h; y++)
                        {
                            long offsetSrc = (y * srcStep);
                            long offsetDst = (y * dstStep);
                            long bytesToCopy = w * ch;

                            Buffer.MemoryCopy(pSrc + offsetSrc, pDst + offsetDst, bytesToCopy, bytesToCopy);
                        }

                        break;

                    default:
                        throw new NotImplementedException();
                }

                hdata.Free();
            }
            finally
            {
                if (bd != null)
                    dst.UnlockBits(bd);
            }
        }
        #endregion
    }
}
