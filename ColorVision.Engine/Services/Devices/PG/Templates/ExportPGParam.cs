﻿#pragma warning disable CS8603  

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.PG.Templates
{
    public class ExportPGParam : IMenuItem
    {
        public Visibility Visibility => Visibility.Visible;

        public string OwnerGuid => "Template";

        public string? GuidId => "PGParam";
        public int Order => 11;
        public string? Header => Properties.Resources.MenuPG;

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(a =>
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplatePGParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }
}