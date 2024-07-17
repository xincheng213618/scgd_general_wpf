#pragma warning disable CA1707,IDE1006

using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Templates;
using cvColorVision;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.Engine.ThirdPartyAlgorithms.Devices.ThirdPartyAlgorithms.Templates.FindDotsArray
{

    public class FindDotsArrayParam : ParamBase
    {

        public FindDotsArrayParam() { }

        public FindDotsArrayParam(ModThirdPartyAlgorithmsModel modThirdPartyAlgorithmsModel)
        {
            Id = modThirdPartyAlgorithmsModel.Id;
            Name = modThirdPartyAlgorithmsModel.Name ?? string.Empty;
            ModThirdPartyAlgorithmsModel = modThirdPartyAlgorithmsModel;
        }

        public ModThirdPartyAlgorithmsModel ModThirdPartyAlgorithmsModel { get; set; }
    }
}
