﻿using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI.Comply;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI.POIFilters
{
    /// <summary>
    /// EditLEDStripDetection.xaml 的交互逻辑
    /// </summary>
    public partial class EditPOIFilters : UserControl
    {
        public EditPOIFilters()
        {
            InitializeComponent();
        }
        public POIFilterParam Param { get; set; }

        public void SetParam(POIFilterParam param)
        {
            Param = param;
            this.DataContext = Param;
        }

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            ComboBoxXYZType.ItemsSource = from e1 in Enum.GetValues(typeof(XYZType)).Cast<XYZType>() select new KeyValuePair<XYZType, string>(e1, e1.ToString());
            ComboBoxXYZType.SelectedIndex = 0;
        }


    }
}