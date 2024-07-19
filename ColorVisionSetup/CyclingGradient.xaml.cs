using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ColorVisionSetup
{
	public partial class CyclingGradient : UserControl, IComponentConnector
	{
		private static TimeSpan _ts1 = new TimeSpan(0, 0, 0, 0, 0);

		private static TimeSpan _ts2 = new TimeSpan(0, 0, 0, 1, 500);

		private static TimeSpan _ts3 = new TimeSpan(0, 0, 0, 3, 0);

		private static double _gs2Start = -0.6;

		private static double _gs2End = 1.0;

		private static double _gs3Start = 0.0;

		private static double _gs3End = 1.6;

		private static Duration _segmentDuration = new Duration(new TimeSpan(0, 0, 0, 1, 500));

		private static Duration _animDuration = new Duration(new TimeSpan(0, 0, 0, 4, 500));

		public new static readonly DependencyProperty OpacityMaskProperty = DependencyProperty.Register("OpacityMask", typeof(Brush), typeof(CyclingGradient));

		public static readonly DependencyProperty Color1Property = DependencyProperty.Register("Color1", typeof(Color), typeof(CyclingGradient), new PropertyMetadata(Color.FromArgb(byte.MaxValue, 0, 149, byte.MaxValue)));

		public static readonly DependencyProperty Color2Property = DependencyProperty.Register("Color2", typeof(Color), typeof(CyclingGradient), new PropertyMetadata(Color.FromArgb(byte.MaxValue, 0, 230, 156)));

		public static readonly DependencyProperty Color3Property = DependencyProperty.Register("Color3", typeof(Color), typeof(CyclingGradient), new PropertyMetadata(Color.FromArgb(byte.MaxValue, 160, 71, byte.MaxValue)));

		public new Brush OpacityMask
		{
			get
			{
				return (Brush)GetValue(OpacityMaskProperty);
			}
			set
			{
				SetValue(OpacityMaskProperty, value);
			}
		}

		public Color Color1
		{
			get
			{
				return (Color)GetValue(Color1Property);
			}
			set
			{
				if (value != (Color)GetValue(Color1Property))
				{
					SetValue(Color1Property, value);
					onColorChange();
				}
			}
		}

		public Color Color2
		{
			get
			{
				return (Color)GetValue(Color2Property);
			}
			set
			{
				if (value != (Color)GetValue(Color2Property))
				{
					SetValue(Color2Property, value);
					onColorChange();
				}
			}
		}

		public Color Color3
		{
			get
			{
				return (Color)GetValue(Color3Property);
			}
			set
			{
				if (value != (Color)GetValue(Color3Property))
				{
					SetValue(Color3Property, value);
					onColorChange();
				}
			}
		}

		public CyclingGradient()
		{
			InitializeComponent();
            Loaded += onLoaded;
		}

		private void onLoaded(object sender, RoutedEventArgs e)
		{
			onColorChange();
		}

		private void onColorChange()
		{
			Storyboard storyboard = storyBoardControl;
			storyboard.Duration = _animDuration;
			storyboard.Children.Clear();
			DoubleAnimation doubleAnimation = new DoubleAnimation(_gs2Start, _gs2End, _segmentDuration)
			{
				BeginTime = _ts1
			};
			Storyboard.SetTargetName(doubleAnimation, "GradientStop2");
			Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath(GradientStop.OffsetProperty));
			storyboard.Children.Add(doubleAnimation);
			DoubleAnimation doubleAnimation2 = new DoubleAnimation(_gs3Start, _gs3End, _segmentDuration)
			{
				BeginTime = _ts1
			};
			Storyboard.SetTargetName(doubleAnimation2, "GradientStop3");
			Storyboard.SetTargetProperty(doubleAnimation2, new PropertyPath(GradientStop.OffsetProperty));
			storyboard.Children.Add(doubleAnimation2);
			DoubleAnimation doubleAnimation3 = new DoubleAnimation(_gs2Start, _gs2End, _segmentDuration)
			{
				BeginTime = _ts2
			};
			Storyboard.SetTargetName(doubleAnimation3, "GradientStop2");
			Storyboard.SetTargetProperty(doubleAnimation3, new PropertyPath(GradientStop.OffsetProperty));
			storyboard.Children.Add(doubleAnimation3);
			DoubleAnimation doubleAnimation4 = new DoubleAnimation(_gs3Start, _gs3End, _segmentDuration)
			{
				BeginTime = _ts2
			};
			Storyboard.SetTargetName(doubleAnimation4, "GradientStop3");
			Storyboard.SetTargetProperty(doubleAnimation4, new PropertyPath(GradientStop.OffsetProperty));
			storyboard.Children.Add(doubleAnimation4);
			DoubleAnimation doubleAnimation5 = new DoubleAnimation(_gs2Start, _gs2End, _segmentDuration)
			{
				BeginTime = _ts3
			};
			Storyboard.SetTargetName(doubleAnimation5, "GradientStop2");
			Storyboard.SetTargetProperty(doubleAnimation5, new PropertyPath(GradientStop.OffsetProperty));
			storyboard.Children.Add(doubleAnimation5);
			DoubleAnimation doubleAnimation6 = new DoubleAnimation(_gs3Start, _gs3End, _segmentDuration)
			{
				BeginTime = _ts3
			};
			Storyboard.SetTargetName(doubleAnimation6, "GradientStop3");
			Storyboard.SetTargetProperty(doubleAnimation6, new PropertyPath(GradientStop.OffsetProperty));
			storyboard.Children.Add(doubleAnimation6);
			ColorAnimationUsingKeyFrames colorAnimationUsingKeyFrames = new ColorAnimationUsingKeyFrames
			{
				Duration = _animDuration,
				KeyFrames = 
				{
					(ColorKeyFrame)new DiscreteColorKeyFrame(Color2, _ts1),
					(ColorKeyFrame)new DiscreteColorKeyFrame(Color3, _ts2),
					(ColorKeyFrame)new DiscreteColorKeyFrame(Color1, _ts3)
				}
			};
			Storyboard.SetTargetName(colorAnimationUsingKeyFrames, "GradientStop1");
			Storyboard.SetTargetProperty(colorAnimationUsingKeyFrames, new PropertyPath(GradientStop.ColorProperty));
			storyboard.Children.Add(colorAnimationUsingKeyFrames);
			ColorAnimationUsingKeyFrames colorAnimationUsingKeyFrames2 = new ColorAnimationUsingKeyFrames
			{
				Duration = _animDuration,
				KeyFrames = 
				{
					(ColorKeyFrame)new DiscreteColorKeyFrame(Color2, _ts1),
					(ColorKeyFrame)new DiscreteColorKeyFrame(Color3, _ts2),
					(ColorKeyFrame)new DiscreteColorKeyFrame(Color1, _ts3)
				}
			};
			Storyboard.SetTargetName(colorAnimationUsingKeyFrames2, "GradientStop2");
			Storyboard.SetTargetProperty(colorAnimationUsingKeyFrames2, new PropertyPath(GradientStop.ColorProperty));
			storyboard.Children.Add(colorAnimationUsingKeyFrames2);
			ColorAnimationUsingKeyFrames colorAnimationUsingKeyFrames3 = new ColorAnimationUsingKeyFrames
			{
				Duration = _animDuration,
				KeyFrames = 
				{
					(ColorKeyFrame)new DiscreteColorKeyFrame(Color1, _ts1),
					(ColorKeyFrame)new DiscreteColorKeyFrame(Color2, _ts2),
					(ColorKeyFrame)new DiscreteColorKeyFrame(Color3, _ts3)
				}
			};
			Storyboard.SetTargetName(colorAnimationUsingKeyFrames3, "GradientStop3");
			Storyboard.SetTargetProperty(colorAnimationUsingKeyFrames3, new PropertyPath(GradientStop.ColorProperty));
			storyboard.Children.Add(colorAnimationUsingKeyFrames3);
			ColorAnimationUsingKeyFrames colorAnimationUsingKeyFrames4 = new ColorAnimationUsingKeyFrames
			{
				Duration = _animDuration,
				KeyFrames = 
				{
					(ColorKeyFrame)new DiscreteColorKeyFrame(Color1, _ts1),
					(ColorKeyFrame)new DiscreteColorKeyFrame(Color2, _ts2),
					(ColorKeyFrame)new DiscreteColorKeyFrame(Color3, _ts3)
				}
			};
			Storyboard.SetTargetName(colorAnimationUsingKeyFrames4, "GradientStop4");
			Storyboard.SetTargetProperty(colorAnimationUsingKeyFrames4, new PropertyPath(GradientStop.ColorProperty));
			storyboard.Children.Add(colorAnimationUsingKeyFrames4);
			storyboard.Begin(GradientBG, isControllable: true);
		}
	}
}
