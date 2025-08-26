using ColorVision.Engine.Abstractions;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.Compliance
{
    public class ViewHandleComplianceJND : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get;  } = new List<ViewResultAlgType>()
        {
            ViewResultAlgType.Compliance_Math_JND,
        };

        public override void Handle(AlgorithmView view, ViewResultAlg result)
        {
            view.ImageView.ImageShow.Clear();


            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults == null)
            {
                result.ViewResults = ComplianceJNDDao.Instance.GetAllByPid(result.Id).ToViewResults();
            }
            List<string> header;
            List<string> bdHeader;
            header = new() { "名称", "h_jnd", "v_jnd", "Validate", "ValidateString" };
            bdHeader = new() { "Name", "DataValueH", "DataValueV", "Validate", "ValidateResult" };

            if (view.listViewSide.View is GridView gridView)
            {
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.listViewSide.ItemsSource = result.ViewResults;
            }
        }
    }
}
