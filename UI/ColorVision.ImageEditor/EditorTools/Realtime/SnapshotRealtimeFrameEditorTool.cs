using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Realtime;
using System;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.Realtime
{
    public sealed class SnapshotRealtimeFrameEditorTool : IEditorTool
    {
        private readonly RealtimeEditorContext _editorContext;

        public SnapshotRealtimeFrameEditorTool(RealtimeEditorContext editorContext)
        {
            _editorContext = editorContext;
            Command = new RelayCommand(_ => SaveCurrentFrame());
        }

        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;

        public string? GuidId => nameof(SnapshotRealtimeFrameEditorTool);

        public int Order => 7;

        public object Icon { get; } = IEditorToolFactory.TryFindResource("DrawingImageCamera");

        public ICommand? Command { get; }

        private void SaveCurrentFrame()
        {
            RealtimeImageViewService realtime = _editorContext.Realtime;
            RealtimeFrameSnapshot? rawSnapshot = realtime.CaptureCurrentFrame();

            if (rawSnapshot == null && realtime.CaptureDisplayedBitmap() == null)
            {
                MessageBox.Show("当前没有可保存的实时帧。", "保存实时帧", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            using System.Windows.Forms.SaveFileDialog dialog = new()
            {
                Filter = "当前帧 PNG (*.png)|*.png|当前帧 RAW (*.raw)|*.raw|显示图 PNG (*.png)|*.png",
                FileName = "realtime-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"),
                AddExtension = true,
                RestoreDirectory = true,
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            try
            {
                if (dialog.FilterIndex == 2)
                {
                    if (rawSnapshot == null)
                    {
                        MessageBox.Show("当前没有原始实时帧可保存。", "保存实时帧", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    rawSnapshot.SaveRaw(dialog.FileName);
                    return;
                }

                if (dialog.FilterIndex == 3)
                {
                    if (!realtime.SaveDisplayedPng(dialog.FileName))
                    {
                        MessageBox.Show("当前没有显示图可保存。", "保存实时帧", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    return;
                }

                if (rawSnapshot != null)
                {
                    rawSnapshot.SavePng(dialog.FileName);
                }
                else
                {
                    realtime.SaveDisplayedPng(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存实时帧失败: " + ex.Message, "保存实时帧", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
