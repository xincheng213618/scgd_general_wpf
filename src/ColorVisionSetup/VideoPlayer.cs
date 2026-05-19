using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVisionSetup
{
	public class VideoPlayer : Image
	{
		private enum CODECSMANAGER_STATUS
		{
			CODECSMANAGER_E_FAIL,
			CODECSMANAGER_E_OK,
			CODECSMANAGER_E_NO_FRAMES
		}

		public delegate void createDelegate();

		public delegate void destroyDelegate();

		public delegate ulong webp_open_decoderDelegate(IntPtr data, int data_size);

		public delegate ulong webp_close_decoderDelegate(ulong hndl);

		public delegate int webp_get_frameDelegate(ulong hndl, IntPtr buffer, int buffer_size, out int timestamp);

		public delegate void webp_resetDelegate(ulong hndl);

		public delegate int webp_get_infoDelegate(byte[] data, int data_size, out int width, out int height);

		public delegate int webp_get_anim_infoDelegate(ulong hdnl, out int width, out int height, out int frame_count, out int loop_count, out int bgcolor);

		private DispatcherTimer _animationTimer;

		private ulong _decoderHandle;

		private object _bufferlock = new object();

		private IntPtr _unmanagedVideoData;

		private IntPtr _unmanagedBuffer;

		private byte[] _buffer;

		private int _bufferSize;

		private int _loopCounter;

		private int _prevTimestamp;

		private int _width;

		private int _height;

		private int _frameCount;

		private int _loopCount;

		private int _bgcolor;

		public new static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(Uri), typeof(VideoPlayer));

		public static readonly DependencyProperty PlaybackSpeedProperty = DependencyProperty.Register("PlaybackSpeed", typeof(float), typeof(VideoPlayer), new PropertyMetadata(1f));

		public static createDelegate create;

		public static destroyDelegate destroy;

		public static webp_open_decoderDelegate webp_open_decoder;

		public static webp_close_decoderDelegate webp_close_decoder;

		public static webp_get_frameDelegate webp_get_frame;

		public static webp_resetDelegate webp_reset;

		public static webp_get_infoDelegate webp_get_info;

		public static webp_get_anim_infoDelegate webp_get_anim_info;

		public new Uri Source
		{
			get
			{
				return (Uri)GetValue(SourceProperty);
			}
			set
			{
				SetValue(SourceProperty, value);
				onSourceChanged();
			}
		}

		public float PlaybackSpeed
		{
			get
			{
				return (float)GetValue(PlaybackSpeedProperty);
			}
			set
			{
				SetValue(PlaybackSpeedProperty, value);
			}
		}

		public int VideoWidth => _width;

		public int VideoHeight => _height;

		public int FrameCount => _frameCount;

		public int LoopCount => _loopCount;

		public Color BackgroundColor
		{
			get
			{
				byte a = (byte)((_bgcolor >> 24) & 0xFF);
				byte r = (byte)((uint)(_bgcolor >> 16) & 0xFFu);
				byte g = (byte)((uint)(_bgcolor >> 8) & 0xFFu);
				byte b = (byte)((uint)_bgcolor & 0xFFu);
				return Color.FromArgb(a, r, g, b);
			}
		}

		public VideoPlayer()
		{
			create();
            Loaded += onLoaded;
			_animationTimer = new DispatcherTimer(DispatcherPriority.Render, Application.Current.Dispatcher);
			_animationTimer.Tick += animationTick;
			_animationTimer.Interval = new TimeSpan(0, 0, 0, 0, 30);
		}

		~VideoPlayer()
		{
			cleanUp();
			destroy();
		}

		private void cleanUp()
		{
			if (_decoderHandle != 0L)
			{
				webp_close_decoder(_decoderHandle);
			}
			_decoderHandle = 0uL;
			if (_unmanagedBuffer != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(_unmanagedBuffer);
			}
			if (_unmanagedVideoData != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(_unmanagedVideoData);
			}
		}

		private void onLoaded(object sender, RoutedEventArgs e)
		{
			RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.LowQuality);
            SizeChanged += onSizeChanged;
			onSourceChanged();
		}

		private void onSourceChanged()
		{
			lock (_bufferlock)
			{
				_animationTimer.Stop();
				cleanUp();
				Uri uri = (Uri)GetValue(SourceProperty);
				if (!(uri != null))
				{
					return;
				}
				Stream stream = Application.GetResourceStream(uri).Stream;
				if (stream == null)
				{
					Console.WriteLine("Failed to open resource uri");
					return;
				}
				byte[] array = new byte[stream.Length];
				stream.Read(array, 0, array.Length);
				_unmanagedVideoData = Marshal.AllocHGlobal((int)stream.Length);
				Marshal.Copy(array, 0, _unmanagedVideoData, array.Length);
				_decoderHandle = webp_open_decoder(_unmanagedVideoData, array.Length);
				if (webp_get_anim_info(_decoderHandle, out _width, out _height, out _frameCount, out _loopCount, out _bgcolor) == 0)
				{
					cleanUp();
					Console.WriteLine("Failed to parse video file");
					return;
				}
				_bufferSize = _width * _height * 4;
				_unmanagedBuffer = Marshal.AllocHGlobal(_bufferSize);
				_buffer = new byte[_bufferSize];
				_loopCounter = 0;
				_animationTimer.Start();
			}
		}

		private void onSizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (_decoderHandle != 0L)
			{
				render();
			}
		}

		private void animationTick(object sender, EventArgs e)
		{
			lock (_bufferlock)
			{
				while (true)
				{
					int timestamp;
					switch ((CODECSMANAGER_STATUS)webp_get_frame(_decoderHandle, _unmanagedBuffer, _bufferSize, out timestamp))
					{
					case CODECSMANAGER_STATUS.CODECSMANAGER_E_OK:
						Marshal.Copy(_unmanagedBuffer, _buffer, 0, _bufferSize);
						if (_prevTimestamp != 0)
						{
							int num = (int)((float)(timestamp - _prevTimestamp) * PlaybackSpeed);
							if (num > 0)
							{
								_animationTimer.Interval = new TimeSpan(0, 0, 0, 0, num);
							}
						}
						_prevTimestamp = timestamp;
						goto end_IL_0011;
					case CODECSMANAGER_STATUS.CODECSMANAGER_E_NO_FRAMES:
						break;
					default:
						goto end_IL_0011;
					}
					webp_reset(_decoderHandle);
					_loopCounter++;
					if (_loopCounter == _loopCount)
					{
						_loopCounter = 0;
						_animationTimer.Stop();
						break;
					}
					continue;
					end_IL_0011:
					break;
				}
			}
			render();
		}

		private void render()
		{
			lock (_bufferlock)
			{
				base.Source = BitmapSource.Create(_width, _height, 96.0, 96.0, PixelFormats.Bgra32, null, _buffer, _width * 4);
			}
		}

		public static void Initialize()
		{
			try
			{
				create = Core.GetDLLFunction<createDelegate>("logi_codecs_shared.dll", "create");
				destroy = Core.GetDLLFunction<destroyDelegate>("logi_codecs_shared.dll", "destroy");
				webp_open_decoder = Core.GetDLLFunction<webp_open_decoderDelegate>("logi_codecs_shared.dll", "webp_open_decoder");
				webp_close_decoder = Core.GetDLLFunction<webp_close_decoderDelegate>("logi_codecs_shared.dll", "webp_close_decoder");
				webp_get_frame = Core.GetDLLFunction<webp_get_frameDelegate>("logi_codecs_shared.dll", "webp_get_frame");
				webp_reset = Core.GetDLLFunction<webp_resetDelegate>("logi_codecs_shared.dll", "webp_reset");
				webp_get_info = Core.GetDLLFunction<webp_get_infoDelegate>("logi_codecs_shared.dll", "webp_get_info");
				webp_get_anim_info = Core.GetDLLFunction<webp_get_anim_infoDelegate>("logi_codecs_shared.dll", "webp_get_anim_info");
			}
			catch (Exception ex)
			{
			}
		}
	}
}
