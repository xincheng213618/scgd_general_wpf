namespace ColorVision.Engine.Templates.Aoi
{
    public class TemplateAOIParam : ITemplate<AOIParam>, IITemplateLoad
    {
        public TemplateAOIParam()
        {
            Title = "AOIParam设置";
            Code = ModMasterType.Aoi;
            TemplateParams = AOIParam.Params;
        }
    }
}
