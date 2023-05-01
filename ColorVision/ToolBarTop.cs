using ColorVision.MVVM;
using Gu.Wpf.Geometry;
using System;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace ColorVision
{
    public class ToolBarTop: ViewModelBase
    {
        public RelayCommand ZoomUniformToFill { get; set; }
        public RelayCommand ZoomUniform { get; set; }
        public RelayCommand ZoomIncrease { get; set; }
        public RelayCommand ZoomDecrease { get; set; }
        public RelayCommand ZoomNone { get; set; }



        private ZoomboxSub ZoomboxSub { get; set; }

        public ToolBarTop(ZoomboxSub zombox)
        {
            this.ZoomboxSub = zombox ?? throw new ArgumentNullException(nameof(zombox));
            ZoomUniformToFill = new RelayCommand(a => ZoomboxSub.ZoomUniformToFill());
            ZoomUniform = new RelayCommand(a => ZoomboxSub.ZoomUniform());
            ZoomIncrease = new RelayCommand(a => ZoomboxSub.Zoom(1.25));
            ZoomDecrease = new RelayCommand(a => ZoomboxSub.Zoom(0.8));
            ZoomNone = new RelayCommand(a => ZoomboxSub.ZoomNone());
        }

        /// <summary>
        /// 当前的缩放分辨率
        /// </summary>
        public double ZoomRatio
        {
            get => ZoomboxSub.ContentMatrix.M11;
            set => ZoomboxSub.Zoom(value);
        }



        private bool _EraseVisual;
        public bool EraseVisual
        {
            get => _EraseVisual;
            set
            {
                if (_EraseVisual != value)
                {
                    _EraseVisual = value;
                    NotifyPropertyChanged();
                }
            }
        }   

    }
}
