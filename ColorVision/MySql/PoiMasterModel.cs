using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql
{
    internal class PoiMasterModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }

        public int Type { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int LeftTopX { get; set; }
        public int LeftTopY { get; set; }
        public int RightTopX { get; set; }
        public int RightTopY { get; set; }
        public int RightBottomX { get; set; }
        public int RightBottomY { get; set; }
        public int LeftBottomX { get; set; }
        public int LeftBottomY { get; set; }
        public bool IsDynamics { get; set; }
        public DateTime CreateDate { get; set; }
        public bool IsEnable { get; set; }
        public bool IsDelete { get; set; }
        public string? Remark { get; set; }
    }
}
