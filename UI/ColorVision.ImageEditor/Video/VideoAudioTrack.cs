using System;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Video
{
    internal interface IVideoAudioTrack
    {
        TimeSpan Position { get; set; }
        double Speed { get; set; }
        bool IsMuted { get; set; }
        void Open(string filePath);
        void Play();
        void Pause();
        void Close();
    }

    // Called only by the playback session on its Dispatcher.
    internal sealed class WpfVideoAudioTrack : IVideoAudioTrack
    {
        private MediaPlayer? _player;

        public TimeSpan Position
        {
            get => _player?.Position ?? TimeSpan.Zero;
            set { if (_player != null) _player.Position = value; }
        }

        public double Speed
        {
            get => _player?.SpeedRatio ?? 1;
            set { if (_player != null) _player.SpeedRatio = value; }
        }

        public bool IsMuted
        {
            get => _player?.IsMuted ?? false;
            set { if (_player != null) _player.IsMuted = value; }
        }

        public void Open(string filePath)
        {
            Close();
            _player = new MediaPlayer();
            try
            {
                _player.Open(new Uri(filePath, UriKind.Absolute));
                _player.Pause();
            }
            catch
            {
                Close();
                throw;
            }
        }

        public void Play() => _player?.Play();
        public void Pause() => _player?.Pause();

        public void Close()
        {
            MediaPlayer? player = _player;
            _player = null;
            if (player == null) return;
            try { player.Stop(); }
            finally { player.Close(); }
        }
    }
}
