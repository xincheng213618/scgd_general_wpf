#pragma warning disable CS8603  

using ColorVision.Engine.Templates;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.PG.Templates
{

    public class PGParam : ParamModBase
    {

        public PGParam()
        {
        }

        public PGParam(ModMasterModel modMaster, List<ModDetailModel> pgDetail) : base(modMaster, pgDetail)
        {

        }

        public const string StartKey = "CM_StartPG";
        public const string StopKey = "CM_StopPG";
        public const string ReSetKey = "CM_ReSetPG";
        public const string SwitchUpKey = "CM_SwitchUpPG";
        public const string SwitchDownKey = "CM_SwitchDownPG";
        public const string SwitchFrameKey = "CM_SwitchFramePG";
        public const string CustomKey = "CM_CustomCmd";
        //PG开始指令
        public string StartPG
        {
            set { SetProperty(ref _CM_StartPG, value, StartKey); }
            get => GetValue(_CM_StartPG, StartKey);
        }
        private string _CM_StartPG;
        //PG停止指令
        public string StopPG
        {
            set { SetProperty(ref _CM_StopPG, value, StopKey); }
            get => GetValue(_CM_StopPG, StopKey);
        }
        private string _CM_StopPG;
        //PG重置指令
        public string ReSetPG
        {
            set { SetProperty(ref _CM_ReSetPG, value, ReSetKey); }
            get => GetValue(_CM_ReSetPG, ReSetKey);
        }
        private string _CM_ReSetPG;
        //PG上指令
        public string SwitchUpPG
        {
            set { SetProperty(ref _CM_SwitchUpPG, value, SwitchUpKey); }
            get => GetValue(_CM_SwitchUpPG, SwitchUpKey);
        }
        private string _CM_SwitchUpPG;

        //PG下指令
        public string SwitchDownPG
        {
            set { SetProperty(ref _CM_SwitchDownPG, value, SwitchDownKey); }
            get => GetValue(_CM_SwitchDownPG, SwitchDownKey);
        }
        private string _CM_SwitchDownPG;
        //PG切指定指令
        public string SwitchFramePG
        {
            set { SetProperty(ref _CM_SwitchFramePG, value, SwitchFrameKey); }
            get => GetValue(_CM_SwitchFramePG, SwitchFrameKey);
        }
        private string _CM_SwitchFramePG;

        public Dictionary<string, string> ConvertToMap()
        {
            Dictionary<string, string> result = new();
            result.Add(StartKey, StartPG);
            result.Add(StopKey, StopPG);
            result.Add(ReSetKey, ReSetPG);
            result.Add(SwitchUpKey, SwitchUpPG);
            result.Add(SwitchDownKey, SwitchDownPG);
            result.Add(SwitchFrameKey, SwitchFramePG);
            result.Add(CustomKey, "");
            return result;
        }
    }
}
