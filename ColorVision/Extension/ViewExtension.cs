﻿using ColorVision.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xceed.Wpf.Toolkit.Zoombox;

namespace ColorVision.Extension
{
    class MyException : Exception
    {
        public MyException(string msg) { }
    }

    public static class ViewExtension
    {
        public static void AddViewConfig(this UserControl userControl, IView view, ComboBox comboBox)
        {
#pragma warning disable CA2201 // 不要引发保留的异常类型
            if (view is not Control control) throw new Exception("View is not Control");
#pragma warning restore CA2201 // 不要引发保留的异常类型

            ViewGridManager.GetInstance().AddView(control);


            void ViewMaxChangedEvent(int max)
            {
                List<KeyValuePair<string, int>> KeyValues = new List<KeyValuePair<string, int>>
                {
                    new KeyValuePair<string, int>(Properties.Resource.WindowSingle, -2),
                    new KeyValuePair<string, int>(Properties.Resource.WindowHidden, -1)
                };
                for (int i = 0; i < max; i++)
                {
                    KeyValues.Add(new KeyValuePair<string, int>((i + 1).ToString(), i));
                }
                comboBox.ItemsSource = KeyValues;
                comboBox.SelectedValue = view.View.ViewIndex;
            }

            ViewMaxChangedEvent(ViewGridManager.GetInstance().ViewMax);
            ViewGridManager.GetInstance().ViewMaxChangedEvent += ViewMaxChangedEvent;

            //移除掉所有的订阅者
            view.View.ClearViewIndexChangedSubscribers();

            view.View.ViewIndexChangedEvent += (e1, e2) =>
            {
                comboBox.SelectedIndex = e2 + 2;
            };
            comboBox.SelectionChanged += (s, e) =>
            {
                if (s is ComboBox combo && combo.SelectedItem is KeyValuePair<string, int> KeyValue)
                {
                    view.View.ViewIndex = KeyValue.Value;
                    ViewGridManager.GetInstance().SetViewIndex(control, KeyValue.Value);
                }
            };
            view.View.ViewIndex = -1;

            userControl.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (ViewConfig.GetInstance().IsAutoSelect)
                {
                    if (ViewGridManager.GetInstance().ViewMax == 1)
                    {
                        view.View.ViewIndex = 0;
                        ViewGridManager.GetInstance().SetViewIndex(control, 0);
                    }
                }
            };

        }

    }
}
