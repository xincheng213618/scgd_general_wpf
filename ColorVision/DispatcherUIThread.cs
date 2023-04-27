using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision
{

    /// <summary>
    /// 管理 DynamicRenderer 线程
    /// </summary>
    public class DispatcherUIThread : IDisposable
    {
        /// <summary>
        /// 启动新的线程
        /// </summary>
        public void Start()
        {
            _syncThreadStartResetEvent = new AutoResetEvent(false);
            var dynamicRendererThread = new Thread(InternalRunning)
            {
                Priority = ThreadPriority.Highest,
                IsBackground = true,
            };
            //设置线程为单线程
            dynamicRendererThread.SetApartmentState(ApartmentState.STA);
            dynamicRendererThread.Start();
            //等待Dispatcher已启动
            _syncThreadStartResetEvent.WaitOne();
            _syncThreadStartResetEvent.Dispose();
            _syncThreadStartResetEvent = null;
        }

        private AutoResetEvent _syncThreadStartResetEvent;

        /// <summary>
        /// 运行笔的线程
        /// </summary>
        public Dispatcher Dispatcher { get; private set; }

        [SecurityCritical]
        private void InternalRunning()
        {
            Dispatcher = Dispatcher.CurrentDispatcher;
            _syncThreadStartResetEvent.Set();
            //开启消息循环
            Dispatcher.Run();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Close();
            Dispatcher = null;
        }

        /// <summary>
        /// 关闭线程
        /// </summary>
        public void Close()
        {
            Dispatcher.InvokeShutdown();
        }
    }

    //异步UI容器
    public class AsyncUIContainer : UIElement
    {
        public AsyncUIContainer()
        {
            Initialize();
        }

        public HostVisual hostVisual;
        ContainerVisual Container;
        Rect _hitTestRect = Rect.Empty;

        /// <summary>
        /// 
        /// </summary>
        void Initialize()
        {
            //定义主线程UI宿主
            hostVisual = new HostVisual();
            //开启UI线程
            var thread = new DispatcherUIThread();
            thread.Start();

            thread.Dispatcher.Invoke(() =>
            {
                //在新的UI线程中，初始化Visual容器
                Container = new ContainerVisual();
                //先此线程的UI和主线程UI衔接
                new VisualTarget(hostVisual)
                {
                    RootVisual = Container
                };
            });
            //将UI宿主，添加到视觉树
            AddVisualChild(hostVisual);
        }

        //因为是多线程上的UI，所以需要使用该UI所在线程才可调度
        public Dispatcher UIDispatcher
        {
            get
            {
                return Container.Dispatcher;
            }
        }

        public VisualCollection Children
        {
            get
            {
                return Container.Children;
            }
        }

        //重写命中测试，让多线程UI能够响应基础输入
        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            if (_hitTestRect.Contains(hitTestParameters.HitPoint))
            {
                return new PointHitTestResult(this, hitTestParameters.HitPoint);
            }

            return base.HitTestCore(hitTestParameters);
        }

        //重写布局系统，让多线程UI元素能正常布局
        protected override Size MeasureCore(Size availableSize)
        {
            if (availableSize.Height > 0 && availableSize.Width > 0 && !(double.IsInfinity(availableSize.Height) || double.IsInfinity(availableSize.Width)))
            {
                return availableSize;
            }

            return new Size(200, 200);
        }

        //重写布局系统，让多线程UI元素能正常布局
        protected override void ArrangeCore(Rect finalRect)
        {
            base.ArrangeCore(finalRect);

            RenderSize = finalRect.Size;
            _hitTestRect = finalRect;
        }

        //指定起Visual的数量
        protected override int VisualChildrenCount => 1;
        //指定需要渲染的Visual对象
        protected override Visual GetVisualChild(int index)
        {
            return hostVisual;
        }
    }

}
