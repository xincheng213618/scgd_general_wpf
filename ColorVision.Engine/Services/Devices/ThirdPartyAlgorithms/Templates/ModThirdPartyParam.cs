#pragma warning disable CA1707,IDE1006

using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Templates;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates
{

    public class ModThirdPartyParam : ParamBase
    {

        public ModThirdPartyParam() 
        {
        }

        public ModThirdPartyParam(ModThirdPartyAlgorithmsModel modThirdPartyAlgorithmsModel)
        {
            ModThirdPartyAlgorithmsModel = modThirdPartyAlgorithmsModel;
        }

        public override int Id { get => ModThirdPartyAlgorithmsModel.Id; set { ModThirdPartyAlgorithmsModel.Id = value; NotifyPropertyChanged(); } }
        public override string Name { get => ModThirdPartyAlgorithmsModel.Name ?? string.Empty; set { ModThirdPartyAlgorithmsModel.Name = value; NotifyPropertyChanged(); } }
        public ModThirdPartyAlgorithmsModel ModThirdPartyAlgorithmsModel { get; set; }

        public string JsonValue
        {
            get => BeautifyJson(ModThirdPartyAlgorithmsModel.JsonVal); set
            {
                if (IsValidJson(value))
                {
                    ModThirdPartyAlgorithmsModel.JsonVal = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private static string BeautifyJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            try
            {
                var parsedJson = JToken.Parse(json);
                return parsedJson.ToString(Formatting.Indented);
            }
            catch (JsonReaderException)
            {
                // If parsing fails, return the original string
                return json;
            }
        }

        private static bool IsValidJson(string strInput)
        {
            if (string.IsNullOrWhiteSpace(strInput))
            {
                return false;
            }

            strInput = strInput.Trim();
            if ((strInput.StartsWith('{') && strInput.EndsWith('}')) || // For object
                (strInput.StartsWith('[') && strInput.EndsWith(']')))   // For array
            {
                try
                {
                    var obj = JToken.Parse(strInput);
                    return true;
                }
                catch (JsonReaderException jex)
                {
                    // Exception in parsing json
                    Console.WriteLine(jex.Message);
                    return false;
                }
                catch (Exception ex) // Some other exception
                {
                    Console.WriteLine(ex.ToString());
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

    }
}
