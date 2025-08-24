using ColorVision.Engine.Abstractions;
using ColorVision.Database;
using Newtonsoft.Json;
using System.IO;

namespace ColorVision.Engine.Templates.Jsons.Distortion2
{
    public class Distortion2View : IViewResult
    {
        public Distortion2View()
        {
        }
        public Distortion2View(DetailCommonModel detail)
        {
            var restfile = JsonConvert.DeserializeObject<ResultFile>(detail.ResultJson);
            if (restfile != null)
            {
                if (File.Exists(restfile.ResultFileName))
                {
                    string json = File.ReadAllText(restfile.ResultFileName);
                    Result = json;
                    DistortionReslut = JsonConvert.DeserializeObject<DistortionReslut>(json);
                }
            }
            else
            {
                Result = detail.ResultJson;
            }
        }

        public string Result { get; set; }

        public DistortionReslut DistortionReslut { get; set; }







    }








}
