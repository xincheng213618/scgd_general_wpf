﻿using ColorVision.Database;
using ColorVision.Themes;
using System;
using System.Windows;

namespace ColorVision.Engine.Templates
{
    /// <summary>
    /// EditConfig.xaml 的交互逻辑
    /// </summary>
    public partial class TemplateSettingEdit : Window
    {
        public static TemplateSetting Config => TemplateSetting.Instance;
        public ITemplate ITemplate { get; set; }
        public TemplateSettingEdit(ITemplate template)
        {
            ITemplate = template;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
        }

        private void Mysql_Reset_Click(object sender, RoutedEventArgs e)
        {
            if (ITemplate.GetMysqlCommand() is IMysqlCommand mysqlCommand)
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), $"是否重置数据库{mysqlCommand.GetMysqlCommandName()}相关项", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    MySqlControl.GetInstance().BatchExecuteNonQuery(mysqlCommand.GetRecover());
                }
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"没有配置数据库重置选项", "ColorVision");
            }
        }
    }
}
