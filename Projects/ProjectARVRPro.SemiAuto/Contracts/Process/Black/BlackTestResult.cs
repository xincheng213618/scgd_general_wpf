using ColorVision.Common.MVVM;
using ProjectARVRPro.Process;
using System.Collections.Generic;

namespace ProjectARVRPro.Process.Black
{
    /// <summary>
    /// 黑场测试结果。
    /// </summary>
    public class BlackTestResult : ViewModelBase
    {
        /// <summary>测点光色数据列表。</summary>
        public List<PoixyuvData> PoixyuvDatas { get; set; } = new List<PoixyuvData>();
        /// <summary>FOFO 对比度，表示白场亮度与黑场亮度的对比关系，单位 %。</summary>
        public ObjectiveTestItem FOFOContrast { get; set; } = new ObjectiveTestItem { Name = "FOFOContrast", Unit = "%" };
    }
}
