﻿using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsServicePlugin
{
    public class PluginWindowService : IPluginBase
    {
        public override string Header { get; set; } = "服务插件";
        public override string UpdateUrl { get; set; }
        public override string Description { get; set; } = "增强的服务管理，比如服务日志等";
    }
}