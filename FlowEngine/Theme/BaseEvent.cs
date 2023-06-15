#pragma warning disable CA1010
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Collections;
using System.ComponentModel;
using System.Windows.Markup;
using System.Windows.Controls;

namespace FlowEngine.Theme
{
    public partial class BaseEvent : ResourceDictionary
    {
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
            }
        }


        public void NumberValidationTextBox(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Back || e.Key == Key.Left || e.Key == Key.Right)
            {
                e.Handled = false;
                return;
            }
            if ((e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) || e.Key == Key.Decimal)
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }
    }
}
