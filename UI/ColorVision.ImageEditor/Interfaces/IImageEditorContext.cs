#pragma warning disable CS1591
using System;
using System.Threading.Tasks;
using System.Windows.Media;
using ColorVision.Core;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// ͼ��༭���������ģ��ݽӿڣ���
    /// �� UI �ؼ��ӿڣ�����¶������״̬�����ڽ�������ԡ�
    /// </summary>
    public interface IImageEditorContext
    {
        // ����״̬
        ImageViewConfig Config { get; }

        /// <summary>
        /// ��ǰ��ʾͼ��ͨ����Ӧ ViewBitmapSource����ֻ�����ʡ�
        /// </summary>
        ImageSource? Source { get; }

        /// <summary>
        /// ��ǰ����ͼ����ʱЧ��Ԥ������ֻ�����ʡ�
        /// </summary>
        ImageSource? FunctionImage { get; }

        /// <summary>
        /// ��ǰͼ��ĵײ� HImage ���棨�����ã���
        /// </summary>
        HImage? HImage { get; }

        // ��������
        /// <summary>
        /// �����µ�Դͼ��ˢ�� UI�������Զ���մ���ͼ����
        /// </summary>
        void SetImageSource(ImageSource imageSource);

        /// <summary>
        /// Ӧ�ô�����ͼ��ΪԤ��ͼ��FunctionImage�������л���ʾ��
        /// </summary>
        void ApplyProcessedImage(ImageSource imageSource);

        /// <summary>
        /// �򻭲���ӿ��Ӷ��󣨵��Ӳ㣩��
        /// </summary>
        void AddVisual(Visual visual);

        /// <summary>
        /// �ӻ����Ƴ����Ӷ��󣨵��Ӳ㣩��
        /// </summary>
        void RemoveVisual(Visual visual);

        /// <summary>
        /// ���ݵ�ǰͼ�����ŵ�״̬���±��/������ʾ��
        /// </summary>
        void UpdateZoomAndScale();

        // �߳�/��������
        /// <summary>
        /// �� UI �߳�ִ�С�
        /// </summary>
        void RunOnUI(Action action);

        /// <summary>
        /// �ں�̨�߳���������
        /// </summary>
        void RunBackground(Func<Task> action);

        // �¼�
        /// <summary>
        /// �� Source �� FunctionImage �����л�/���ʱ������
        /// </summary>
        event EventHandler? ImageChanged;

        /// <summary>
        /// ���Ӳ�����¼���
        /// </summary>
        event EventHandler<Visual>? VisualAdded;

        /// <summary>
        /// ���Ӳ��Ƴ��¼���
        /// </summary>
        event EventHandler<Visual>? VisualRemoved;
    }
}
