using ColorVision.Engine.Abstractions;
using ColorVision.Database;
using Newtonsoft.Json;
using SqlSugar;
using System.IO;

namespace ColorVision.Engine.Templates.Jsons.FOV2
{
    public class CameraFOV
    {
        public double D_Fov { get; set; }
        public double H_Fov { get; set; }
        public double V_FOV { get; set; }
        public double ClolorVisionH_Fov { get; set; }
        public double ClolorVisionV_Fov { get; set; }
        public double LeftDownToRightUp { get; set; }
        public double LeftUpToRightDown { get; set; }
    }
    public class ResDFov
    {
        public CameraFOV result { get; set; }
    }

    public class DFovView : IViewResult
    {
        public DFovView()
        {

        }

        public DFovView(DetailCommonModel detail)
        {
            var restfile = JsonConvert.DeserializeObject<ResultFile>(detail.ResultJson);
            if (restfile != null)
            {
                if (File.Exists(restfile.ResultFileName))
                {
                    string json = File.ReadAllText(restfile.ResultFileName);
                    Result = JsonConvert.DeserializeObject<ResDFov>(json);
                    D_Fov = Result.result.D_Fov;
                    H_Fov = Result.result.H_Fov;
                    V_FOV = Result.result.V_FOV;
                    ClolorVisionH_Fov = Result.result.ClolorVisionH_Fov;
                    ClolorVisionV_Fov = Result.result.ClolorVisionV_Fov;
                    LeftDownToRightUp = Result.result.LeftDownToRightUp;
                    LeftUpToRightDown = Result.result.LeftUpToRightDown;
                }
            }


        }

        public ResDFov Result { get; set; }

        public double D_Fov { get; set; }
        public double H_Fov { get; set; }
        public double V_FOV { get; set; }
        public double ClolorVisionH_Fov { get; set; }
        public double ClolorVisionV_Fov { get; set; }
        public double LeftDownToRightUp { get; set; }
        public double LeftUpToRightDown { get; set; }


    }



}
