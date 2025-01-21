using ColorVision.Solution.V;
using ColorVision.Solution.V.Files;
using ColorVision.UI;
using log4net;
using System.IO;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Solution
{
    public class CommadnInitialized : IMainWindowInitialized
    {
        public Task Initialize()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                Application.Current.MainWindow.CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (s, e) => SolutionManager.GetInstance().NewCreateWindow(), (s, e) => e.CanExecute = true));
                Application.Current.MainWindow.CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (s, e) => SolutionManager.GetInstance().OpenSolutionWindow(), (s, e) => e.CanExecute = true));
            });
            return Task.CompletedTask;
        }
    }

    public partial class TreeViewControl
    {
        private void IniCommand()
        {
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ExecutedCommand, CanExecuteCommand));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut, ExecutedCommand, CanExecuteCommand));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, ExecutedCommand, CanExecuteCommand));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, ExecutedCommand, CanExecuteCommand));
            CommandBindings.Add(new CommandBinding(Commands.ReName, ExecutedCommand, CanExecuteCommand));

        }

        #region 通用命令执行函数
        private void CanExecuteCommand(object sender, CanExecuteRoutedEventArgs e)
        {
            if (e.Parameter is VObject baseObject)
            {
                if (e.Command == ApplicationCommands.SelectAll)
                {
                    e.CanExecute = false;
                }   
                else if (e.Command == ApplicationCommands.Copy)
                {
                    e.CanExecute = true;
                }
                else if (e.Command == ApplicationCommands.Cut)
                {
                    e.CanExecute = true;
                }
                else if (e.Command == ApplicationCommands.Paste)
                {
                    e.CanExecute = true;
                }
                else if (e.Command == ApplicationCommands.Delete)
                {


                    e.CanExecute = baseObject.CanDelete;
                }
                else if (e.Command == Commands.ReName)
                {
                    e.CanExecute = baseObject.CanReName;
                }
            }
            else if (SelectedTreeViewItem != null && SelectedTreeViewItem.DataContext is VObject baseObject1)
            {
                if (e.Command == ApplicationCommands.SelectAll)
                {
                    e.CanExecute = false;
                }
                else if (e.Command == ApplicationCommands.Copy)
                {
                    e.CanExecute = baseObject1.CanCopy;
                }
                else if (e.Command == ApplicationCommands.Cut)
                {
                    e.CanExecute = false;
                }
                else if (e.Command == ApplicationCommands.Paste)
                {
                    e.CanExecute = Clipboard.ContainsData("VObjectFormat");
                }
                else if (e.Command == ApplicationCommands.Delete)
                {
                    e.CanExecute = baseObject1.CanDelete;
                }
                else if (e.Command == Commands.ReName)
                {
                    e.CanExecute = baseObject1.CanReName;
                }
            }
        }



        private void ExecutedCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Copy)
            {
                if (e.Parameter is VObject baseObject)
                {
                    DataContractSerializer serializer = new DataContractSerializer(typeof(VObject));

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        serializer.WriteObject(memoryStream, baseObject);
                        byte[] objectData = memoryStream.ToArray();

                        // 将字节数组放入剪贴板
                        Clipboard.SetData("VObjectFormat", objectData);
                    }
                }
                //this.DoCopy();
            }
            else if (e.Command == ApplicationCommands.Cut)
            {
                //this.DoCut();
            }
            else if (e.Command == ApplicationCommands.Paste)
            {
                if (Clipboard.ContainsData("VObjectFormat"))
                {
                    byte[] objectData = (byte[])Clipboard.GetData("VObjectFormat");
                    using (MemoryStream memoryStream = new MemoryStream(objectData))
                    {
                        DataContractSerializer serializer = new DataContractSerializer(typeof(VObject));
                        VObject baseObject = (VObject)serializer.ReadObject(memoryStream);
                    }
                }
            }
            else if (e.Command == ApplicationCommands.Delete)
            {
                if (e.Parameter != null)
                {
                    if (e.Parameter is VObject baseObject)
                    {
                        baseObject.Parent.RemoveChild(baseObject);
                    }
                }
                else
                {
                    if (SelectedTreeViewItem != null)
                    {
                        if (SelectedTreeViewItem.DataContext is VFile vFile)
                        {
                            vFile.Delete();
                        }
                        else if (SelectedTreeViewItem.DataContext is VObject baseObject)
                        {
                            baseObject.Parent?.RemoveChild(baseObject);
                        }
                    }
                }
            }
            else if (e.Command == Commands.ReName)
            {
                if (e.Parameter != null)
                {
                    if (e.Parameter is VObject baseObject)
                    {
                        LastReNameObject = baseObject;
                        baseObject.IsEditMode = true;
                    }
                }
                else
                {
                    //没有数据的时候通过点击确认
                    if (SelectedTreeViewItem != null)
                    {
                        if (SelectedTreeViewItem.DataContext is VObject baseObject)
                        {
                            LastReNameObject = baseObject;
                            baseObject.IsEditMode = true;
                        }
                    }
                }
            }
            else
            {

            }

        }

        #endregion
    }

}