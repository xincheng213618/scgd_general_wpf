﻿using ColorVision.Common.Utilities;
using ColorVision.Solution.Editor;
using ColorVision.Solution.Searches;
using System.IO;
using System.Windows;
using WpfHexaEditor.Core;
using Xceed.Wpf.AvalonDock.Layout;

namespace ColorVision.Solution
{
    // 标记本类支持的扩展名，并设为默认
    [GenericEditor("Hex文本编辑器")]
    public class HexEditor : EditorBase
    {
        public override void Open(string filePath)
        {
            if (File.Exists(filePath))
            {
                string GuidId = Tool.GetMD5(filePath);
                var existingDocument = SolutionViewExtensions.FindDocumentById(SolutionViewExtensions.layoutRoot, GuidId.ToString());

                if (existingDocument != null)
                {
                    if (existingDocument.Parent is LayoutDocumentPane layoutDocumentPane)
                    {
                        layoutDocumentPane.SelectedContentIndex = layoutDocumentPane.IndexOf(existingDocument); ;
                    }
                    else if (existingDocument.Parent is LayoutFloatingWindow layoutFloatingWindow)
                    {
                        var window = Window.GetWindow(layoutFloatingWindow);
                        if (window != null)
                        {
                            window.Activate();
                        }
                    }
                }
                else
                {
                    var HexEditor = new WpfHexaEditor.HexEditor();
                    HexEditor.PreloadByteInEditorMode = PreloadByteInEditor.MaxVisibleLineExtended;
                    HexEditor.FileName = filePath;


                    LayoutDocument layoutDocument = new LayoutDocument() { ContentId = GuidId, Title = Path.GetFileName(filePath) };
                    layoutDocument.Content = HexEditor;
                    SolutionViewExtensions.LayoutDocumentPane.Children.Add(layoutDocument);
                    SolutionViewExtensions.LayoutDocumentPane.SelectedContentIndex = SolutionViewExtensions.LayoutDocumentPane.IndexOf(layoutDocument);
                    layoutDocument.IsActiveChanged += (s, e) =>
                    {
                        if (layoutDocument.IsActive)
                        {
                            SolutionViewExtensions.OnContentIdSelected(filePath);
                        }
                    };
                    layoutDocument.Closing += (s, e) =>
                    {
                        HexEditor.CloseProvider(true);
                        HexEditor.Dispose();
                    };
                }
            }
        }
    }
}