using System.Collections.Generic;

using Newtonsoft.Json;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.Flow
{
    /// <summary>
    /// 流程引擎模板
    /// </summary>
    public class FlowParam : ParamModBase
    {
        public FlowParam() 
        {

        }

        public FlowParam(ModMasterModel dbModel, List<ModDetailModel> flowDetail) : base(dbModel, flowDetail)
        {
            _DataBase64 = flowDetail.Count >0 ? flowDetail[0].Value ?? string.Empty : string.Empty;
        }

        public string DataBase64 { get => _DataBase64; set { _DataBase64 = value; } }
        private string _DataBase64;

        /// <summary>
        /// Runtime-only identity of the resource that owns <see cref="DataBase64"/>.
        /// These members are deliberately excluded from template serialization so
        /// the existing STN/CVFlow contracts remain unchanged.
        /// </summary>
        [Browsable(false)]
        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public int? ResourceId { get; internal set; }

        [Browsable(false)]
        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string? ResourceCode { get; internal set; }

        [Browsable(false)]
        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string? FlowKey { get; internal set; }

        [Browsable(false)]
        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public int? TemplateRevision { get; internal set; }

        [Browsable(false)]
        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string? TemplateContentHash { get; internal set; }

        [Browsable(false)]
        [JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string? LoadedContentHash { get; internal set; }

    }
}
