﻿using ColorVision.Database;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using ColorVision.Engine.Services;

namespace ColorVision.Engine.Templates.Compliance
{
    public class ViewHandleComplianceY : IResultHandleBase
    {
        public override List<ViewResultAlgType> CanHandle { get;  } = new List<ViewResultAlgType>()
        {
            ViewResultAlgType.Compliance_Contrast,
            ViewResultAlgType.Compliance_Math,
            ViewResultAlgType.Compliance_Contrast_CIE_Y,
            ViewResultAlgType.Compliance_Math_CIE_Y,
        };

        public override void Handle(IViewImageA view, ViewResultAlg result)
        {
            view.ImageView.ImageShow.Clear();



            if (File.Exists(result.FilePath))
                view.ImageView.OpenImage(result.FilePath);

            if (result.ViewResults == null)
            {
                result.ViewResults = ComplianceYDao.Instance.GetAllByPid(result.Id).ToViewResults();
            }
            List<string> header;
            List<string> bdHeader;
            header = new() { "名称", "值", "Validate" };
            bdHeader = new() { "Name", "DataValue", "ValidateResult" };

            if (view.ListView.View is GridView gridView)
            {
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.ListView.ItemsSource = result.ViewResults;
            }
        }
    }
}
