﻿#pragma warning disable CA1707,IDE1006

using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.ImageCropping
{
    public class PointFloat:ViewModelBase
    {
        public float X { get => _X; set { _X = value; NotifyPropertyChanged(); } }
        private float _X;
        public float Y { get => _Y; set { _Y = value; NotifyPropertyChanged(); } }
        private float _Y;
    }

    public class ImageCroppingParam : ParamBase
    {
        public ImageCroppingParam() { }
        public ImageCroppingParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        [Category("UnEgde"), Description("UnEgde")]
        public int UnEgde { get => GetValue(_UnEgde); set { SetProperty(ref _UnEgde, value); } }
        private int _UnEgde = 1;


    }
}
