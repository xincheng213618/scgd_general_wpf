using Newtonsoft.Json;

namespace ColorVision.Services.Msg
{
    public class ParamFunction
    {
        public string Name { get; set; }
        [JsonProperty("params")]
        public dynamic Params { get; set; }
        public override string ToString() => JsonConvert.SerializeObject(this);
    }     
}
