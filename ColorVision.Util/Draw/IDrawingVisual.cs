﻿#pragma warning disable CA1711,CA2211
using System.Windows.Media;

namespace ColorVision.Draw
{
    public interface IDrawingVisual
    {
        public abstract DrawBaseAttribute GetAttribute();
        public bool AutoAttributeChanged { get; set; }

        public Pen Pen { get; set; }

        public abstract void Render();

    }



}
