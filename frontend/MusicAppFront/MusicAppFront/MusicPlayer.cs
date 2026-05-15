using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MusicAppFront.Models.SearchResultDto;

namespace MusicAppFront
{
    public static class GlobalPlayer
    {
        private static LibVLC _libVLC;
        private static MediaPlayer _vlcPlayer;
        private static bool _isInitialized = false;
        public static event Action OnPlayingStarted;
        public static event Action OnPlayingPaused;
        public static event Action OnTrackEnded;

        public static event Action OnTrackChanged;
        public static event Action<long> OnTimeChanged; // Текущее время в мс
        public static event Action<long> OnLengthChanged;

        public static TrackDto2 CurrentTrack { get; set; }
        

        public static void Init()
        {
            if (_isInitialized) return;
            try
            {
                Core.Initialize();

                // Упрощаем список опций. Иногда --audio-resampler вызывает крэш, если нет нужной DLL
                var options = new string[]
                {
            "--network-caching=3000",
            "--no-video",
            "--ignore-config" // Игнорировать локальные настройки VLC пользователя
                };

                _libVLC = new LibVLC(options);

                if (_libVLC != null)
                {
                    _vlcPlayer = new MediaPlayer(_libVLC);
                    _isInitialized = true;
                    Debug.WriteLine("[VLC] Инициализация успешна");
                }

                _vlcPlayer.LengthChanged += (s, e) => {
                    // e.Length — это длительность в миллисекундах
                    OnLengthChanged?.Invoke(e.Length);
                };

                _vlcPlayer.TimeChanged += (s, e) => {
                    // e.Time — текущая позиция в миллисекундах
                    OnTimeChanged?.Invoke(e.Time);
                };


            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VLC CRITICAL ERROR]: {ex.Message}");
            }
        }

        public static void Play(string url)
        {
            if (!_isInitialized) Init();
            if (_libVLC == null || _vlcPlayer == null)
            {
                Debug.WriteLine("[VLC] Плеер не инициализирован, запуск невозможен.");
                return;
            }

            try
            {
               
                var media = new Media(_libVLC, new Uri(url));

                _vlcPlayer.Playing += (s, e) => {
                    OnPlayingStarted?.Invoke();
                };

                _vlcPlayer.Paused += (s, e) =>
                {
                    OnPlayingPaused?.Invoke();
                };

                _vlcPlayer.EndReached += (s, e) =>
                {
                    // VLC вызывает это в своем потоке, так что просто кидаем сигнал
                    OnTrackEnded?.Invoke();
                };

                var uri = new Uri(url.Replace(" ", "%20"));

                _vlcPlayer.Media = media;
                _vlcPlayer.Play();
                OnTrackChanged?.Invoke();
                Debug.WriteLine($"[VLC PLAYER] Стрим запущен: {url}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VLC PLAY ERROR]: {ex.Message}");
            }
        }

        public static void Seek(float position)
        {
            if (_vlcPlayer != null && _vlcPlayer.IsSeekable)
            {
                // position должен быть от 0.0 до 1.0
                _vlcPlayer.Position = position;
            }
        }

        public static void TogglePause()
        {
            if (_vlcPlayer == null) return;

            if (_vlcPlayer.IsPlaying)
            {
                _vlcPlayer.SetPause(true); // Принудительно ставим на паузу
            }
            else
            {
                _vlcPlayer.Play(); // Запускаем
            }
        }

        // Убедись, что это свойство есть
        public static bool IsPlaying => _vlcPlayer != null && _vlcPlayer.IsPlaying;


        public static void Pause() => _vlcPlayer?.Pause();
        public static void Resume() => _vlcPlayer?.Play();
        public static void Stop() => _vlcPlayer?.Stop();
    }
}
