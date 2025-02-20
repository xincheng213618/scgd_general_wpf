using ColorVision.Solution.V;
using ColorVision.Solution.V.Files;
using ColorVision.UI;
using System.IO;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Solution
{
    public partial class TreeViewControl
    {
        private void IniCommand()
        {
            SolutionTreeView.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ExecutedCommand, CanExecuteCommand));
            SolutionTreeView.CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut, ExecutedCommand, CanExecuteCommand));
            SolutionTreeView.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, ExecutedCommand, CanExecuteCommand));

            SolutionTreeView.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete,   (s,e)=>
            {
                if (SelectedTreeViewItem?.DataContext is VObject baseObject) baseObject.Delete();
            }
            , (s, e) => e.CanExecute = SelectedTreeViewItem != null && SelectedTreeViewItem.DataContext is VObject baseObject && baseObject.CanDelete));


            SolutionTreeView.CommandBindings.Add(new CommandBinding(Commands.ReName, (s, e) =>
            {
                if (SelectedTreeViewItem != null && SelectedTreeViewItem.DataContext is VObject baseObject)
                    baseObject.IsEditMode = true;
            }, (s, e) => e.CanExecute = SelectedTreeViewItem != null && SelectedTreeViewItem.DataContext is VObject baseObject && baseObject.CanReName));
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

        }

        #endregion
    }

}