﻿#pragma warning disable CA1707,IDE1006

using ColorVision.Services.Dao;
using ColorVision.Services.Templates;
using System.Collections.Generic;

namespace ColorVision.Services.Devices.Algorithm.Templates
{
    public class FocusPointsParam : ParamBase
    {
        public FocusPointsParam() { }
        public FocusPointsParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
        {
        }

        public bool value1 { get; set; }


    }
}
