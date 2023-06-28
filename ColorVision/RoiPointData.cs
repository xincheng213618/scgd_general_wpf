#pragma warning disable CA1707, CA1051,CS8601,CS8603,CA1051,CA1707


using ColorVision.Config;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace ColorVision
{
    public class RoiPointsCfg
    {
        private static string cfgDefaultFile = "cfg\\RiPointSet.cfg";

        public Dictionary<string, RiPoint> riPointDict;

        public RoiPointsCfg()
        {
            riPointDict = Util.CfgFile.Load<Dictionary<string, RiPoint>>(cfgDefaultFile) ?? new Dictionary<string, RiPoint>() { { "default", new RiPoint() } };
        }

        public bool Save()
        {
            return Util.CfgFile.Save(cfgDefaultFile, riPointDict);
        }

        public RiPoint? GetCurRiPoint()
        {
            foreach (var item in riPointDict)
            {
                if (item.Value.bSelected)
                {
                    return item.Value;
                }
            }
            return default;
        }
    }

    public class RiPoint
    {
        public List<RoiPointData> riPoints;
        public System.Drawing.Point[] LAPoints;
        public LedCheckCfg ledCheckCfg;
        public bool isRealPix;
        public bool enable;

        public int TheNum = 10;

        public int nextId = 1;
        public bool bSelected { set; get; }

        public RiPoint()
        {
            riPoints = new List<RoiPointData>();
            LAPoints = new System.Drawing.Point[4];
            ledCheckCfg = Util.CfgFile.Load<LedCheckCfg>("cfg\\LedCheck.cfg");
        }


        public void Reset()
        {
            riPoints.Clear();
            nextId = 1;
        }

        internal RoiPointData SelectPoint(float scale, Point msPos)
        {
            if (riPoints == null || riPoints.Count == 0) return null;
            RoiPointData result = null;
            int count = riPoints.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                RoiPointData pt = riPoints[i];
                int n_y = (int)(msPos.Y / scale);
                int n_x = (int)(msPos.X / scale);
                if (result == null && pt.IsPointIn(new Point(n_x, n_y)))
                {
                    result = pt;
                }
                else
                {
                    pt.setSelected(false);
                }
            }

            return result;
        }

        internal void MoveSelected(float scale, Point msPos)
        {
            if (riPoints == null || riPoints.Count == 0) return;
            foreach (RoiPointData pt in riPoints)
            {
                if (pt.IsSelected())
                {
                    pt.Move(scale, msPos);
                }
            }
        }

        public void HideData()
        {
            if (riPoints == null || riPoints.Count == 0) return;
            foreach (RoiPointData pt in riPoints)
            {
                pt.HideData();
            }
        }

        internal void CalcVal(float[,] imageData)
        {
            if (riPoints == null || riPoints.Count == 0) return;
            foreach (RoiPointData pt in riPoints)
            {
                pt.CalcVal(imageData);
            }
        }

        internal void CalcVal(ushort[,] imageData)
        {
            if (riPoints == null || riPoints.Count == 0) return;
            foreach (RoiPointData pt in riPoints)
            {
                pt.CalcVal(imageData);
            }
        }
        internal string exportTxt()
        {
            if (riPoints == null || riPoints.Count == 0) return "";
            string result = "名称\tX\tY\t平均值\r\n";
            foreach (RoiPointData pt in riPoints)
            {
                result += pt.exportValToTxt() + "\r\n";
            }
            return result;
        }

        internal RoiPointData GetSelected()
        {
            if (riPoints == null || riPoints.Count == 0) return null;
            foreach (RoiPointData pt in riPoints)
            {
                if (pt.IsSelected()) return pt;
            }

            return null;
        }

        internal string GetNextPointTitle()
        {
            return "Point " + nextId++;
        }
    }

    public enum RiPointTypes
    {
        Circle = 0,
        Rect = 1,
        Mask = 2
    }


    public class RoiPointData
    {
        public bool bShow { set; get; }
        public string title { set; get; }
        public RiPointTypes ptType { set; get; }
        public int Img_x { set; get; }
        public int Img_y { set; get; }
        public int w { set; get; }
        public int h { set; get; }
        public int Real_x { set; get; }
        public int Real_y { set; get; }
        public int Real_w { set; get; }
        public int Real_h { set; get; }
        public string operation { set; get; }

        public double DataCorrect { set; get; }

        public bool bSelected;
        private float mean;
        private float max;
        private float min;

        private bool showData;

        public RoiPointData()
        {
            bShow = true;
        }

        public float getMean()
        {
            return mean;
        }

        public bool IsSelected()
        {
            return bSelected;
        }

        public bool IsPointIn(Point pos)
        {
            bSelected = false;
            if (ptType == RiPointTypes.Circle)
                bSelected = IsPointInCircle(pos);
            else
                bSelected = IsPointInRect(pos);

            return bSelected;
        }

        public bool IsPointInCircle(Point pos) => ((Img_x - pos.X) * (Img_x - pos.X) + (Img_y - pos.Y) * (Img_y - pos.Y)) < w / 2 * h / 2;
        public bool IsPointInRect(Point pos) => Math.Sqrt((Img_x - pos.X) * (Img_x - pos.X) + (Img_y - pos.Y) * (Img_y - pos.Y)) <= w;

        public void HideData()
        {
            showData = false;
        }

        public void CalcVal(float[,] data)
        {
            int d_h = data.GetLength(0);
            int d_w = data.GetLength(1);
            int stx = Img_x - w / 2;
            int sty = Img_y - h / 2;
            double sum = 0;
            UInt32 count = 0;
            float val = 0;
            min = UInt16.MaxValue;
            max = UInt16.MinValue;
            Point pt = new Point();
            for (int i = 0; i < h; i++)
            {
                for (int j = 0; j < w; j++)
                {
                    pt.X = j + stx;
                    pt.Y = i + sty;
                    if (IsPointIn(pt))
                    {
                        val = data[pt.Y, pt.X];
                        sum += val;
                        if (val > max) max = val;
                        if (val < min) min = val;
                        count++;
                    }
                }
            }
            if (count > 0)
            {
                mean = (float)(sum / count);
            }
            showData = true;
        }
        public void CalcVal(UInt16[,] data)
        {
            int d_h = data.GetLength(0);
            int d_w = data.GetLength(1);
            int stx = Img_x - w / 2;
            int sty = Img_y - h / 2;
            UInt64 sum = 0;
            UInt32 count = 0;
            UInt16 val = 0;
            min = UInt16.MaxValue;
            max = UInt16.MinValue;
            Point pt = new Point();
            for (int i = 0; i < h; i++)
            {
                for (int j = 0; j < w; j++)
                {
                    pt.X = j + stx;
                    pt.Y = i + sty;
                    if (IsPointIn(pt))
                    {
                        val = data[pt.Y, pt.X];
                        sum += val;
                        if (val > max) max = val;
                        if (val < min) min = val;
                        count++;
                    }
                }
            }
            if (count > 0)
            {
                mean = sum / count;
            }
            showData = true;
        }

        public void Draw(float scale, Graphics graphics)
        {
            if (bShow)
            {
                int n_y = (int)(scale * (Img_y - h / 2));
                int n_x = (int)(scale * (Img_x - w / 2));
                int n_h = (int)(scale * h);
                int n_w = (int)(scale * w);
                Pen pen = bSelected ? Pens.Yellow : Pens.Red;
                if (ptType == RiPointTypes.Circle)
                    graphics.DrawEllipse(pen, new Rectangle(n_x, n_y, n_w, n_h));
                else
                    graphics.DrawRectangle(pen, new Rectangle(n_x, n_y, n_w, n_h));
                int ypos = n_y + n_h / 2 - 5;
                Font font = new Font("Verdana", 8);
                SolidBrush brush = new SolidBrush(Color.Red);
                graphics.DrawString(title, font, brush, new PointF(n_x, ypos));
                if (showData)
                {
                    ypos += 10;
                    graphics.DrawString(string.Format("平均值:{0:0.0000}", mean), font, brush, new PointF(n_x, ypos));
                }
            }
        }

        internal string exportValToTxt()
        {
            return string.Format("{0}\t{1}\t{2}\t{3:0.0000}", title, Img_x, Img_y, mean);
        }

        internal void setSelected(bool v)
        {
            bSelected = false;
        }

        internal void Move(float scale, Point msPos)
        {
            int n_y = (int)(msPos.Y / scale);
            int n_x = (int)(msPos.X / scale);
            Img_x = n_x;
            Img_y = n_y;
        }

        internal void SetShow(bool value)
        {
            bShow = value;
        }
    }
}
