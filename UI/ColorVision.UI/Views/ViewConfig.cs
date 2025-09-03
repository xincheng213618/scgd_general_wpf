﻿using ColorVision.Common.MVVM;

namespace ColorVision.UI.Views
{

    public class ViewConfig : ViewModelBase, IConfig
    {
        public static ViewConfig Instance => ConfigService.Instance.GetRequiredService<ViewConfig>();

        public bool IsAutoSelect { get => _IsAutoSelect; set { _IsAutoSelect = value; OnPropertyChanged(); } }
        private bool _IsAutoSelect =true;
        public int ViewMaxCount { get => _ViewMaxCount; set { _ViewMaxCount = value; OnPropertyChanged(); } }
        private int _ViewMaxCount = 1;

    }
}
