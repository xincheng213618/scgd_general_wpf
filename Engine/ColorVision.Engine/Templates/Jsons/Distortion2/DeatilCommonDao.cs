using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;
using Newtonsoft.Json;
using System.IO;

namespace ColorVision.Engine.Templates.Jsons.Distortion2
{
    public class Distortion2View : IViewResult
    {
        public Distortion2View(DetailCommonModel detail)
        {
            Id = detail.Id;
            PId = detail.PId;
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
        [Column("id")]
        public int Id { get; set; }
        [Column("pid")]
        public int PId { get; set; }

        [Column("result")]
        public string Result { get; set; }

        public DistortionReslut DistortionReslut { get; set; }







    }








}
