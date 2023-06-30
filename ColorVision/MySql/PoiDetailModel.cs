using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MySql
{
    internal class PoiDetailModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Pid { get; set; }
        public int Type { get; set; }
        public int PixX { get; set; }
        public int PixY { get; set; }
        public int PixWidth { get; set; }
        public int PixHeight { get; set; }
        public DateTime CreateDate { get; set; }
        public bool IsEnable { get; set; }
        public bool IsDelete { get; set; }
        public string? Remark { get; set; }
    }
}
