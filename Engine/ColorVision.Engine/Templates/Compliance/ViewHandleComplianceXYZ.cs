using ColorVision.Database;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.Compliance
{
    public class ViewHandleComplianceXYZ : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get; } = new List<ViewResultAlgType>()
        {
            ViewResultAlgType.Compliance_Contrast_CIE_XYZ,
            ViewResultAlgType.Compliance_Math_CIE_XYZ,
        };

        public override void Handle(ViewResultContext ctx, ViewResultAlg result)
        {
            ctx.ImageView.ImageShow.Clear();

            if (File.Exists(result.FilePath))
                ctx.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults == null)
            {
                result.ViewResults = ComplianceXYZDao.Instance.GetAllByPid(result.Id).ToViewResults();
            }
            List<string> header;
            List<string> bdHeader;
            header = new() { "名称", "值", "Validate" };
            bdHeader = new() { "Name", "DataValue", "ValidateResult" };

            if (ctx.ListView.View is GridView gridView)
            {
                ctx.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                ctx.ListView.ItemsSource = result.ViewResults;
            }
        }
    }
}
