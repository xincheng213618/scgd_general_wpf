﻿using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.Compliance
{
    public class ViewHandleComplianceXYZ : IResultHandleBase
    {
        public override List<AlgorithmResultType> CanHandle { get; } = new List<AlgorithmResultType>()
        {
            AlgorithmResultType.Compliance_Contrast_CIE_XYZ,
            AlgorithmResultType.Compliance_Math_CIE_XYZ,
        };

        public override void Handle(AlgorithmView view, AlgorithmResult result)
        {
            view.ImageView.ImageShow.Clear();
            if (result.ResultCode != 0)
            {
                if (File.Exists(result.FilePath))
                    view.ImageView.OpenImage(result.FilePath);
                return;
            }


            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults == null)
            {
                result.ViewResults = ComplianceXYZDao.Instance.GetAllByPid(result.Id).ToViewResults();
            }
            List<string> header;
            List<string> bdHeader;
            header = new() { "名称", "值", "Validate" };
            bdHeader = new() { "Name", "DataValue", "ValidateResult" };

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
