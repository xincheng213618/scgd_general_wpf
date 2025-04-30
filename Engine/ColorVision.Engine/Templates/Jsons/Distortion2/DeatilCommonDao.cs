using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.Jsons.FOV2;
using CVCommCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

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
