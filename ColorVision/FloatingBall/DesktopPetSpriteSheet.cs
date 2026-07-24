using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.FloatingBall
{
    public enum DesktopPetActivityState
    {
        Idle,
        Running,
        RunningLeft,
        RunningRight,
        Waiting,
        Review,
        Waving,
        Failed,
        Jumping,
    }

    internal readonly record struct DesktopPetSpriteFrame(int Row, int Column, int DurationMilliseconds);

    internal static class DesktopPetAnimationPlan
    {
        private static readonly IReadOnlyList<DesktopPetSpriteFrame> IdleFrames =
        [
            new(0, 0, 1680),
            new(0, 1, 660),
            new(0, 2, 660),
            new(0, 3, 840),
            new(0, 4, 840),
            new(0, 5, 1920),
        ];

        private static readonly Dictionary<DesktopPetActivityState, IReadOnlyList<DesktopPetSpriteFrame>> FramesByState =
            new Dictionary<DesktopPetActivityState, IReadOnlyList<DesktopPetSpriteFrame>>
            {
                [DesktopPetActivityState.Idle] = IdleFrames,
                [DesktopPetActivityState.Running] = CreateRow(7, 6, 120, 220),
                [DesktopPetActivityState.RunningLeft] = CreateRow(2, 8, 120, 220),
                [DesktopPetActivityState.RunningRight] = CreateRow(1, 8, 120, 220),
                [DesktopPetActivityState.Waiting] = CreateRow(6, 6, 150, 260),
                [DesktopPetActivityState.Review] = CreateRow(8, 6, 150, 280),
                [DesktopPetActivityState.Waving] = CreateRow(3, 4, 140, 280),
                [DesktopPetActivityState.Failed] = CreateRow(5, 8, 140, 240),
                [DesktopPetActivityState.Jumping] = CreateRow(4, 5, 140, 280),
            };

        public static IReadOnlyList<DesktopPetSpriteFrame> GetFrames(DesktopPetActivityState state)
        {
            return FramesByState.TryGetValue(state, out var frames) ? frames : IdleFrames;
        }

        public static DesktopPetActivityState ResolveDragState(DesktopPetActivityState currentState, double deltaX)
        {
            if (deltaX >= 4)
                return DesktopPetActivityState.RunningRight;
            if (deltaX <= -4)
                return DesktopPetActivityState.RunningLeft;

            return currentState;
        }

        private static DesktopPetSpriteFrame[] CreateRow(int row, int count, int durationMilliseconds, int lastFrameDurationMilliseconds)
        {
            var frames = new DesktopPetSpriteFrame[count];
            for (var column = 0; column < count; column++)
            {
                frames[column] = new DesktopPetSpriteFrame(
                    row,
                    column,
                    column == count - 1 ? lastFrameDurationMilliseconds : durationMilliseconds);
            }
            return frames;
        }
    }

    internal sealed class DesktopPetSpriteSheet : IDisposable
    {
        public const int ColumnCount = 8;
        public const int Version1RowCount = 9;
        public const int Version2RowCount = 11;

        private readonly SKBitmap _bitmap;
        private readonly Dictionary<(int Row, int Column), BitmapSource> _frameCache = new();
        private readonly object _syncRoot = new();
        private bool _isDisposed;

        private DesktopPetSpriteSheet(SKBitmap bitmap, int rowCount)
        {
            _bitmap = bitmap;
            RowCount = rowCount;
            FrameWidth = bitmap.Width / ColumnCount;
            FrameHeight = bitmap.Height / rowCount;
        }

        public int RowCount { get; }

        public int FrameWidth { get; }

        public int FrameHeight { get; }

        public static DesktopPetSpriteSheet Load(byte[] encodedImage, int spriteVersionNumber)
        {
            ArgumentNullException.ThrowIfNull(encodedImage);
            if (encodedImage.Length == 0 || encodedImage.Length > DesktopPetAssetCatalog.MaximumSpriteSheetBytes)
                throw new InvalidDataException("The desktop pet sprite sheet is empty or too large.");

            var rowCount = spriteVersionNumber == 1 ? Version1RowCount : Version2RowCount;
            var bitmap = SKBitmap.Decode(encodedImage)
                ?? throw new InvalidDataException("The desktop pet sprite sheet could not be decoded.");

            if (bitmap.Width <= 0
                || bitmap.Height <= 0
                || bitmap.Width > 4096
                || bitmap.Height > 4096
                || bitmap.Width % ColumnCount != 0
                || bitmap.Height % rowCount != 0)
            {
                bitmap.Dispose();
                throw new InvalidDataException($"Desktop pet sprite sheets must use an {ColumnCount} x {rowCount} frame grid.");
            }

            return new DesktopPetSpriteSheet(bitmap, rowCount);
        }

        public BitmapSource GetFrame(int row, int column)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (row < 0 || row >= RowCount)
                throw new ArgumentOutOfRangeException(nameof(row));
            if (column < 0 || column >= ColumnCount)
                throw new ArgumentOutOfRangeException(nameof(column));

            lock (_syncRoot)
            {
                if (_frameCache.TryGetValue((row, column), out var cached))
                    return cached;

                var sourceRect = new SKRectI(
                    column * FrameWidth,
                    row * FrameHeight,
                    (column + 1) * FrameWidth,
                    (row + 1) * FrameHeight);
                using var frame = new SKBitmap(new SKImageInfo(
                    FrameWidth,
                    FrameHeight,
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul));
                using (var canvas = new SKCanvas(frame))
                {
                    canvas.Clear(SKColors.Transparent);
                    canvas.DrawBitmap(_bitmap, sourceRect, new SKRect(0, 0, FrameWidth, FrameHeight));
                    canvas.Flush();
                }

                var pixels = new byte[checked(frame.RowBytes * frame.Height)];
                Marshal.Copy(frame.GetPixels(), pixels, 0, pixels.Length);
                var bitmapSource = BitmapSource.Create(
                    FrameWidth,
                    FrameHeight,
                    96,
                    96,
                    PixelFormats.Pbgra32,
                    null,
                    pixels,
                    frame.RowBytes);
                bitmapSource.Freeze();
                _frameCache[(row, column)] = bitmapSource;
                return bitmapSource;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _bitmap.Dispose();
            _frameCache.Clear();
        }
    }

    internal sealed class DesktopPetSpriteAnimator : IDisposable
    {
        private readonly DispatcherTimer _timer = new();
        private readonly Action<BitmapSource> _renderFrame;
        private DesktopPetSpriteSheet? _spriteSheet;
        private IReadOnlyList<DesktopPetSpriteFrame> _frames = DesktopPetAnimationPlan.GetFrames(DesktopPetActivityState.Idle);
        private DesktopPetActivityState _state = DesktopPetActivityState.Idle;
        private DesktopPetActivityState _returnState = DesktopPetActivityState.Idle;
        private int _frameIndex;
        private int _remainingLoops;

        public DesktopPetSpriteAnimator(Action<BitmapSource> renderFrame)
        {
            _renderFrame = renderFrame ?? throw new ArgumentNullException(nameof(renderFrame));
            _timer.Tick += Timer_Tick;
        }

        public bool HasSpriteSheet => _spriteSheet != null;

        public void SetSpriteSheet(DesktopPetSpriteSheet? spriteSheet)
        {
            _timer.Stop();
            _spriteSheet?.Dispose();
            _spriteSheet = spriteSheet;
            _frameIndex = 0;
            if (_spriteSheet != null)
                SetState(_state);
        }

        public void SetState(DesktopPetActivityState state)
        {
            _state = state;
            _returnState = DesktopPetActivityState.Idle;
            _remainingLoops = 0;
            BeginState(state);
        }

        public void PlayTransient(DesktopPetActivityState state, DesktopPetActivityState returnState, int loopCount = 3)
        {
            _state = state;
            _returnState = returnState;
            _remainingLoops = Math.Max(1, loopCount);
            BeginState(state);
        }

        private void BeginState(DesktopPetActivityState state)
        {
            _frames = DesktopPetAnimationPlan.GetFrames(state);
            _frameIndex = 0;
            RenderCurrentFrame();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_spriteSheet == null || _frames.Count == 0)
            {
                _timer.Stop();
                return;
            }

            _frameIndex++;
            if (_frameIndex >= _frames.Count)
            {
                _frameIndex = 0;
                if (_remainingLoops > 0)
                {
                    _remainingLoops--;
                    if (_remainingLoops == 0)
                    {
                        _state = _returnState;
                        BeginState(_returnState);
                        return;
                    }
                }
            }

            RenderCurrentFrame();
        }

        private void RenderCurrentFrame()
        {
            if (_spriteSheet == null || _frames.Count == 0)
            {
                _timer.Stop();
                return;
            }

            var frame = _frames[_frameIndex];
            _renderFrame(_spriteSheet.GetFrame(frame.Row, frame.Column));
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(16, frame.DurationMilliseconds));
            _timer.Start();
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _spriteSheet?.Dispose();
            _spriteSheet = null;
        }
    }
}
