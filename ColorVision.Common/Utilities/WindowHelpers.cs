﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Common.Utilities
{
    public static class WindowHelpers
    {

        public static Window? GetActiveWindow(this Application application)
        {
            foreach (Window window in application.Windows)
                if (window.IsActive) return window;
            return null;
        }

        public static Window? GetActiveWindow()
        {
            foreach (Window window in Application.Current.Windows)
                if (window.IsActive) return window;
            return null;
        }
    }
}