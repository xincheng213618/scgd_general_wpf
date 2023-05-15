#pragma warning disable CA2211,CS8603

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Config
{
    public class LiveCfg
    {
        public static LiveCfg CfgLoadliveCfg(string fileName) => Util.CfgFile.LoadCfgFile<LiveCfg>($"{fileName}");

        [Category("视频窗口参数配置")]
        public int 窗口数 { set; get; }
        [Category("视频窗口参数配置")]
        public int[] 视频窗口坐标X { set; get; }
        [Category("视频窗口参数配置")]
        public int[] 视频窗口坐标Y { set; get; }
        [Category("视频窗口参数配置")]
        public int[] 视频窗口宽W { set; get; }
        [Category("视频窗口参数配置")]
        public int[] 视频窗口高H { set; get; }


    }

}
