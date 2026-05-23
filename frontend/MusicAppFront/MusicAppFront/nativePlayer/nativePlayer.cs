using LibVLCSharp.Shared;
using Microsoft.Web.WebView2;
using MusicAppFront.Models;
using MusicAppFront.Views.Windows;
using Newtonsoft.Json;
//using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using Path = System.IO.Path;
using MusicAppFront.Views.Pages;

namespace testPlayer
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    /// 

    public class NativePlayer
    {
        private int _pushStream;
        private int _stream;
        private HttpClient _client = new HttpClient();
        private DispatcherTimer _timer;
        public bool _isDragging = false;
        private CancellationTokenSource _cts;
        private bool _isPlayerReady = false;
        private bool _isHttpLoading = false;
        private System.Windows.Threading.DispatcherTimer _syncTimer;
        private List<string> _globalHistory = new List<string>();
        public LibVLCSharp.Shared.LibVLC _libVlc;
        public LibVLCSharp.Shared.MediaPlayer _mediaPlayer;
        private string _currentArtist = "";
        private string _currentTrack = "";
        public MusicAppFront.Views.Pages.FullPlayerPage FullPlayerPage { get; set; }





        //private Queue<TrackWithStreamDto> _playbackQueue = new Queue<TrackWithStreamDto>();
        private ConcurrentQueue<TrackWithStreamDto> _playbackQueue = new ConcurrentQueue<TrackWithStreamDto>();


        // Стек истории (те, что уже проиграли, для кнопки Prev)
        private Stack<TrackWithStreamDto> _historyStack = new Stack<TrackWithStreamDto>();
        private Stack<TrackWithStreamDto> _forwardStack = new Stack<TrackWithStreamDto>();

        // То, что играет прямо сейчас
        public TrackWithStreamDto _currentlyPlayingTrack;

        // Токен для отмены фоновых задач (чтобы старые запросы не забивали канал)
        private CancellationTokenSource _preloadCts;

        // DTO-контейнер, объединяющий инфо о треке и его прямую ссылку
        public class TrackWithStreamDto
        {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string ImageUrl { get; set; }
            public string StreamUrl { get; set; }
            public double Duration { get; set; }
            public string YtUrl { get; set; }
            public bool IsResolved { get; set; }
        }

        public class TrackDto2
        {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string CoverImageUrl { get; set; }

            public string Url { get; set; }
            public string CleanArtist { get; set; }
            public string CleanTitle { get; set; }

            // Пустой конструктор обязателен для работы сериализатора
            public TrackDto2() { }

            public TrackDto2(string title, string artist, string url, string cleanArtist, string cleanTitle, string coverImageUrl)
            {
                Title = title;
                Artist = artist;
                Url = url;
                CleanArtist = cleanArtist;
                CoverImageUrl = coverImageUrl;
            }
        }

        private bool _isPlaying = false;
        private SemaphoreSlim _ResolveSemaphore = new SemaphoreSlim(3);
        private Random _random = new Random();


        //        private string[] _resolveServers = {
        //    "http://localhost:8888",//незалогинен
        //    "http://localhost:8889",//незалогинен
        //    "http://localhost:8890",//залогинен
        //    "http://localhost:8891" //залогинен
        //};
        //        private int _serverIndex = 0;


        private readonly string[] _fastServers = { "http://localhost:8888", "http://localhost:8889" }; // незалогиненные
        private readonly string[] _fallbackServers = { "http://localhost:8890", "http://localhost:8891" }; // залогиненные

        private int _fastIndex = 0;
        private int _fallbackIndex = 0;

        private object _serverLock = new object();
        private readonly MainWindow _mainWindow;


        public event Action<bool> PlayerStatusChanged;

        public NativePlayer(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
           
        }

        private string GetServerForAttempt(int attempt)
        {
            lock (_serverLock)
            {
                // Первые попытки (по количеству быстрых серверов) опрашивают быстрые
                if (attempt < _fastServers.Length)
                {
                    var server = _fastServers[_fastIndex % _fastServers.Length];
                    _fastIndex++;
                    return server;
                }
                else // Если быстрые кончились/упали, переключаемся на залогиненные
                {
                    var server = _fallbackServers[_fallbackIndex % _fallbackServers.Length];
                    _fallbackIndex++;
                    return server;
                }
            }
        }

        //private string GetNextServer()
        //{
        //    lock (_serverLock)
        //    {
        //        var server = _resolveServers[_serverIndex % _resolveServers.Length];
        //        _serverIndex++;
        //        return server;
        //    }
        //}

        //private async Task PreloadRecommendationsAsync(string artist, string track, CancellationToken token)
        //{
        //    string currentArtist = artist;
        //    string currentTrack = track;

        //    while (!token.IsCancellationRequested && _playbackQueue.Count < 30)
        //    {
        //        try
        //        {
        //            string currentTrackId = $"{currentArtist.ToLower()} - {currentTrack.ToLower()}";

        //            if (!_globalHistory.Contains(currentTrackId)) _globalHistory.Add(currentTrackId);

        //            var body = new
        //            {
        //                artist = currentArtist,
        //                track = currentTrack,
        //                exclude = _globalHistory.Skip(Math.Max(0, _globalHistory.Count - 40)).ToList()
        //            };
        //            var bodyStr = Newtonsoft.Json.JsonConvert.SerializeObject(body);
        //            System.Diagnostics.Debug.WriteLine($"[preload] отправляем: {bodyStr.Substring(0, Math.Min(200, bodyStr.Length))}");
        //            System.Diagnostics.Debug.WriteLine($"[preload] exclude count: {_globalHistory.Count}, отправляем exclude: {body.exclude.Count}");
        //            string json = string.Empty;
        //            var content = new StringContent(
        //                Newtonsoft.Json.JsonConvert.SerializeObject(body),
        //                System.Text.Encoding.UTF8,
        //                "application/json"
        //            );

        //            using (var response = await _client.PostAsync(
        //                "https://localhost:7296/api/music/GetNextRecommended", content, token))
        //            {
        //                response.EnsureSuccessStatusCode();
        //                json = await response.Content.ReadAsStringAsync();
        //            }

        //            System.Diagnostics.Debug.WriteLine("json^  " + json);

        //            if (token.IsCancellationRequested) break;

        //            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
        //            if (data == null) break;
        //            //System.Diagnostics.Debug.WriteLine("data^  " + (string)data.ToString()); 
        //            double.TryParse((string)data.duration, System.Globalization.NumberStyles.Any,
        //                System.Globalization.CultureInfo.InvariantCulture, out double dur);

        //            var nextTrack = new TrackWithStreamDto
        //            {
        //                Artist = data.artist,
        //                Title = data.title,
        //                ImageUrl = (string)data.imageUrl,
        //                YtUrl = (string)data.streamUrl,
        //                Duration = dur,
        //                IsResolved = false
        //            };

        //            _playbackQueue.Enqueue(nextTrack);
        //            System.Diagnostics.Debug.WriteLine($"[preload] {nextTrack.Artist} - {nextTrack.Title}, очередь: {_playbackQueue.Count}");
        //            //System.Diagnostics.Debug.WriteLine("img url  " + (string)data.ImageUrl.ToString());
        //            System.Diagnostics.Debug.WriteLine("img2 url  " + nextTrack.ImageUrl);
        //            System.Diagnostics.Debug.WriteLine("just url  " + nextTrack.YtUrl);


        //            // резолвим фоном, цикл не ждёт
        //            //_ = Task.Run(async () => {
        //            //    nextTrack.StreamUrl = await ResolveAudioUrlAsync(nextTrack.YtUrl);
        //            //    nextTrack.IsResolved = true;
        //            //    Console.WriteLine($"[preload] резолв готов: {nextTrack.Artist} - {nextTrack.Title}");
        //            //    Console.WriteLine(nextTrack.StreamUrl);
        //            //});

        //            _ = Task.Run(async () =>
        //            {
        //                await _ResolveSemaphore.WaitAsync();
        //                try
        //                {
        //                    await Task.Delay(_random.Next(1000, 3000));
        //                    nextTrack.StreamUrl = await ResolveAudioUrlAsync(nextTrack.YtUrl);
        //                    nextTrack.IsResolved = true;
        //                    System.Diagnostics.Debug.WriteLine($"[preload] резолв готов: {nextTrack.Artist} - {nextTrack.Title}");



        //                }
        //                finally
        //                {
        //                    _ResolveSemaphore.Release();
        //                }
        //            });


        //            currentArtist = nextTrack.Artist;
        //            currentTrack = nextTrack.Title;
        //        }
        //        catch (OperationCanceledException) { break; }
        //        catch (Exception ex)
        //        {
        //            System.Diagnostics.Debug.WriteLine($"[preload] ошибка: {ex.Message}");


        //            await Task.Delay(1000, token);
        //        }
        //    }
        //}

        private async Task PreloadRecommendationsAsync(string artist, string track, CancellationToken token)
        {
            string currentArtist = artist;
            string currentTrack = track;

            while (!token.IsCancellationRequested && _playbackQueue.Count < 30)
            {
                try
                {
                    string currentTrackId = $"{currentArtist.ToLower()} - {currentTrack.ToLower()}";
                    if (!_globalHistory.Contains(currentTrackId)) _globalHistory.Add(currentTrackId);

                    // 1. Собираем историю прослушивания (последние 40 треков)
                    var excludeList = _globalHistory.Skip(Math.Max(0, _globalHistory.Count - 40)).ToList();

                    // 2. ПОДМЕШИВАЕМ РЕЗУЛЬТАТЫ ПОИСКА ИЗ UI В МАССИВ EXCLUDE ДЛЯ СЕРВЕРА
                    // Бэк использует их как фолбэк, если Last.fm выдаст 404 на условный "rodle"
                    if (_mainWindow?.GlobalResults?.Tracks != null)
                    {
                        foreach (var searchTrack in _mainWindow.GlobalResults.Tracks)
                        {
                            string searchTrackKey = $"{searchTrack.Author?.ToLower()} - {searchTrack.Title?.ToLower()}";
                            if (!excludeList.Contains(searchTrackKey))
                            {
                                excludeList.Add(searchTrackKey);
                            }
                        }
                    }

                    var body = new
                    {
                        artist = currentArtist,
                        track = currentTrack,
                        exclude = excludeList // Отправляем объединенный массив
                    };

                    var content = new StringContent(
                        Newtonsoft.Json.JsonConvert.SerializeObject(body),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );

                    string json = string.Empty;
                    using (var response = await _client.PostAsync(
                        "https://localhost:7296/api/music/GetNextRecommended", content, token))
                    {
                        response.EnsureSuccessStatusCode();
                        json = await response.Content.ReadAsStringAsync();
                    }

                    if (token.IsCancellationRequested) break;

                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                    if (data == null) break;

                    double.TryParse((string)data.duration, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double dur);

                    var nextTrack = new TrackWithStreamDto
                    {
                        Artist = data.artist,
                        Title = data.title,
                        ImageUrl = (string)data.imageUrl,
                        YtUrl = (string)data.streamUrl,
                        Duration = dur,
                        IsResolved = false
                    };

                    _playbackQueue.Enqueue(nextTrack);
                    System.Diagnostics.Debug.WriteLine($"[preload] {nextTrack.Artist} - {nextTrack.Title}, очередь: {_playbackQueue.Count}");

                    // 3. УБИРАЕМ ТЯЖЕЛЫЙ СЕМАФОР И РАНДОМНЫЕ ЗАДЕРЖКИ (Task.Delay)
                    // Плеер тупил, потому что семафор заставлял треки ждать по 3 секунды в очереди на резолв.
                    // Запускаем чистый параллельный таск для мгновенного получения ссылок YouTube:
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            nextTrack.StreamUrl = await ResolveAudioUrlAsync(nextTrack.YtUrl);
                            nextTrack.IsResolved = true;
                            System.Diagnostics.Debug.WriteLine($"[preload] резолв готов: {nextTrack.Artist} - {nextTrack.Title}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[preload] Ошибка резолва ссылки YouTube: {ex.Message}");
                        }
                    });

                    currentArtist = nextTrack.Artist;
                    currentTrack = nextTrack.Title;
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[preload] ошибка: {ex.Message}");

                    // Если упала сеть или бэк недоступен, спим 1 секунду перед повторной попыткой, чтобы не вешать UI
                    await Task.Delay(1000, token);
                }
            }
        }




        public async Task PlayTrack(TrackWithStreamDto track, bool addToHistory = true, bool clearForward = true)
        {
            if (track == null) return;

            if (addToHistory && _currentlyPlayingTrack != null)
                _historyStack.Push(_currentlyPlayingTrack);

            if (clearForward)
                _forwardStack.Clear();

            _currentlyPlayingTrack = track;
            System.Diagnostics.Debug.WriteLine("img url  " + track.ImageUrl);

            await _mainWindow.Dispatcher.InvokeAsync(() =>
            {
                _mainWindow.BottomTrackTitle.Text = $"{track.Artist} - {track.Title}";
                _mainWindow.TimelineSlider.Maximum = track.Duration;
                _mainWindow.TimelineSlider.Value = 0;
                _mainWindow.GlobalPlayPauseBtn.Content = "\uE103"; // Две палочки (Пауза)
                _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);


                if (FullPlayerPage != null)
                {
                    // Меняем тексты большого плеера (подставь свои имена элементов из FullPlayerPage.xaml)
                    FullPlayerPage.BIG_TrackTitle.Text = track.Title;
                    FullPlayerPage.BIG_Author.Text = track.Artist;

                    // Если в большом плеере тоже есть слайдер времени:
                    // FullPlayerPage.BigTimelineSlider.Maximum = track.Duration;
                    // FullPlayerPage.BigTimelineSlider.Value = 0;

                    // Меняем иконку большой кнопки на Паузу
                    FullPlayerPage.BIG_GlobalPlayPauseBtn.Content = "\uE103";
                    FullPlayerPage.BIG_GlobalPlayPauseBtn.Padding = new Thickness(0);

                    // Меняем большую обложку
                    if (!string.IsNullOrEmpty(track.ImageUrl))
                    {
                        FullPlayerPage.BIG_TrackImage.Visibility = System.Windows.Visibility.Visible;
                        FullPlayerPage.BIG_TrackImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(track.ImageUrl));
                    }
                    else
                    {
                        FullPlayerPage.BIG_TrackImage.Source = _mainWindow.BottomTrackImage.Source;
                        FullPlayerPage.BIG_TrackImage.Visibility = System.Windows.Visibility.Collapsed;
                    }
                }


                if (!string.IsNullOrEmpty(track.ImageUrl))
                {
                    _mainWindow.BottomTrackImage.Visibility = System.Windows.Visibility.Visible;
                    _mainWindow.BottomTrackImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(track.ImageUrl));
                }
                else
                {
                    // Если картинки нет, можно обратно скрывать элемент
                    _mainWindow.BottomTrackImage.Visibility = System.Windows.Visibility.Collapsed;
                    
                }
            });

            System.Diagnostics.Debug.WriteLine("track.ImageUrl  " + track.ImageUrl);

            _preloadCts?.Cancel();
            _preloadCts = new CancellationTokenSource();

            if (clearForward) // clearForward=true только при ручном выборе
            {
                while (_playbackQueue.TryDequeue(out _)) { }
            }

            if (_playbackQueue.Count < 30)
                _ = PreloadRecommendationsAsync(track.Artist, track.Title, _preloadCts.Token);
            System.Diagnostics.Debug.WriteLine($"[playtrack] StreamUrl: {track.StreamUrl?.Substring(0, Math.Min(60, track.StreamUrl?.Length ?? 0))}");
            PlayWithVlc(track.StreamUrl);
        }





        public void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Пока тянем мышкой, C# сам обновляет текст на основе положения пипки
            if (_isDragging && _mainWindow.TotalTimeText != null & _mainWindow.CurrentTimeText != null)
            {


                _mainWindow.TotalTimeText.Text = $"{FormatTime(_mainWindow.TimelineSlider.Maximum)}";
                _mainWindow.CurrentTimeText.Text = $"{FormatTime(_mainWindow.TimelineSlider.Value)}";
            }

            if (_isDragging && FullPlayerPage != null && FullPlayerPage.BIG_TotalTime != null & FullPlayerPage.BIG_CurrentTime != null)
            {


                FullPlayerPage.BIG_TotalTime.Text = $"{FormatTime(_mainWindow.TimelineSlider.Maximum)}";
                FullPlayerPage.BIG_CurrentTime.Text = $"{FormatTime(_mainWindow.TimelineSlider.Value)}";
            }
        }




        public async Task<string> ResolveAudioUrlAsync(string youtubeUrl)
        {
            int totalServers = _fastServers.Length + _fallbackServers.Length;

            // Цикл идет по общему количеству серверов (2 быстрых + 2 резервных = 4 попытки)
            for (int attempt = 0; attempt < totalServers; attempt++)
            {
                // Передаем номер попытки: 0 и 1 вернут быстрые, 2 и 3 — залогиненные
                string server = GetServerForAttempt(attempt);
                try
                {
                    string apiUrl = $"{server}/?url={Uri.EscapeDataString(youtubeUrl)}";
                    string json = await _client.GetStringAsync(apiUrl);
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                    string url = (string)data.url;

                    if (!string.IsNullOrEmpty(url))
                    {
                        System.Diagnostics.Debug.WriteLine($"[resolve] готово через {server}");
                        return url;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[resolve] {server} упал: {ex.Message}, пробуем следующий");
                }
            }

            System.Diagnostics.Debug.WriteLine("[resolve] все серваки упали");
            return null;
        }


        //private async Task<string> ResolveAudioUrlAsync(string youtubeUrl)
        //{
        //    // пробуем каждый сервак по очереди
        //    for (int attempt = 0; attempt < _resolveServers.Length; attempt++)
        //    {
        //        string server = GetNextServer();
        //        try
        //        {
        //            string apiUrl = $"{server}/?url={Uri.EscapeDataString(youtubeUrl)}";
        //            string json = await _client.GetStringAsync(apiUrl);
        //            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
        //            string url = (string)data.url;

        //            if (!string.IsNullOrEmpty(url))
        //            {
        //                System.Diagnostics.Debug.WriteLine($"[resolve] готово через {server}");
        //                return url;
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            System.Diagnostics.Debug.WriteLine($"[resolve] {server} упал: {ex.Message}, пробуем следующий");
        //        }
        //    }

        //    System.Diagnostics.Debug.WriteLine("[resolve] все серваки упали");
        //    return null;
        //}




        private void PlayWithVlc(string audioUrl)
        {
            if (string.IsNullOrEmpty(audioUrl))
            {
                System.Diagnostics.Debug.WriteLine("[vlc] пустой url");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[vlc] играем: {audioUrl}");

            _mediaPlayer.Stop();
            PlayerStatusChanged?.Invoke(false);

            using (var media = new Media(_libVlc, new Uri(audioUrl)))
            {
                _mediaPlayer.Media = media;
            }

            _mediaPlayer.Play();
            PlayerStatusChanged?.Invoke(true);
            //if (_mediaPlayer.IsPlaying)
            //{

            //    TxtStatus.Text = "VLC is playing";
            //}
            //else { TxtStatus.Text = "VLC stops"; }
        }
        public async void BtnPlay_Click(object sender, RoutedEventArgs e,SearchResultDto results)
        {

            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Pause();
                _mainWindow.GlobalPlayPauseBtn.Content = "\uE102"; // Треугольник (Плей)
                _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(2, 0, 0, 0);

                if (FullPlayerPage != null)
                {
                    FullPlayerPage.BIG_GlobalPlayPauseBtn.Content = "\uE102"; // Треугольник (Плей)
                    FullPlayerPage.BIG_GlobalPlayPauseBtn.Padding = new Thickness(2, 0, 0, 0);
                }




                var currentPlaying = _currentlyPlayingTrack;
                if (currentPlaying != null && results.Tracks != null)
                {
                    foreach (var track in results.Tracks)
                    {
                        // Сверяем название и автора (как в твоем методе клика)
                        bool isMatch = string.Equals(track.Title?.Trim(), currentPlaying.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(track.Author?.Trim(), currentPlaying.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (isMatch)
                        {
                            // Нашли! Меняем флаг в "сырых" данных до инициализации UI
                            track.IsPlaying = false;
                            break; // Выходим из цикла
                        }
                    }
                }




                return;
            }
            if (_mediaPlayer.Media != null && !_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Play();
                PlayerStatusChanged?.Invoke(true);
                _mainWindow.GlobalPlayPauseBtn.Content = "\uE103"; // Две палочки (Пауза)
                _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);

                FullPlayerPage.BIG_GlobalPlayPauseBtn.Content = "\uE103"; // Две палочки (Пауза)
                FullPlayerPage.BIG_GlobalPlayPauseBtn.Padding = new Thickness(0);


                var currentPlaying = _currentlyPlayingTrack;
                if (currentPlaying != null && results.Tracks != null)
                {
                    foreach (var track in results.Tracks)
                    {
                        // Сверяем название и автора (как в твоем методе клика)
                        bool isMatch = string.Equals(track.Title?.Trim(), currentPlaying.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(track.Author?.Trim(), currentPlaying.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (isMatch)
                        {
                            // Нашли! Меняем флаг в "сырых" данных до инициализации UI
                            track.IsPlaying = true;
                            break; // Выходим из цикла
                        }
                    }
                }

                return;
            }

            //_mainWindow.BottomTrackTitle.Text = "загрузка...";
            ////BtnPlay.IsEnabled = false;

            //string artist = "Judas Priest", track = "Painkiller";
            //string url = $"https://localhost:7296/api/music/stream?artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(track)}";
            //System.Diagnostics.Debug.WriteLine("utl  " + url);
            //string json = await _client.GetStringAsync(url);
            //var data = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(json);

            //if (data != null && data.Count >= 2)
            //{
            //    double.TryParse(data[1], System.Globalization.NumberStyles.Any,
            //        System.Globalization.CultureInfo.InvariantCulture, out double duration);

            //    var firstTrack = new TrackWithStreamDto
            //    {
            //        Artist = artist,
            //        Title = track,
            //        YtUrl = data[0],
            //        Duration = duration,
            //        IsResolved = false
            //    };

            //    firstTrack.StreamUrl = await ResolveAudioUrlAsync(firstTrack.YtUrl);
            //    firstTrack.IsResolved = true;

            //    await PlayTrack(firstTrack);
            //    _mainWindow.GlobalPlayPauseBtn.Content = "\uE103"; // Две палочки (Пауза)
            //    _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);
            //    //BtnPlay.IsEnabled = true;
            //}

        }






        public string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                return "00:00";

            TimeSpan t = TimeSpan.FromSeconds(seconds);

            // Если трек идет дольше часа, выводим часы, иначе просто Минуты:Секунды
            return t.TotalHours >= 1
                ? t.ToString(@"hh\:mm\:ss")
                : t.ToString(@"mm\:ss");
        }







        public async void BtnPrev_Click(object sender, RoutedEventArgs e, SearchResultDto results)
        {
            if (_historyStack.Count == 0)
            {
                //_mainWindow.BottomTrackTitle.Text = "Это самый первый трек";
                return;
            }

            try
            {
                //BtnPrev.IsEnabled = false;

                // Текущий трек отправляем в историю "будущего"
                if (_currentlyPlayingTrack != null)
                    _forwardStack.Push(_currentlyPlayingTrack);

                // Достаем трек из прошлого
                var previousTrack = _historyStack.Pop();

                if (previousTrack != null && results.Tracks != null)
                {
                    foreach (var track in results.Tracks)
                    {
                        // Сверяем название и автора (как в твоем методе клика)
                        bool isMatch = string.Equals(track.Title?.Trim(), previousTrack.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(track.Author?.Trim(), previousTrack.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (isMatch)
                        {
                            // Нашли! Меняем флаг в "сырых" данных до инициализации UI
                            track.IsPlaying = true;
                            break; // Выходим из цикла
                        }
                    }
                }


                // Играем его. 
                // false -> не добавлять в историю прошлого повторно (мы его оттуда только что взяли)
                // false -> НЕ очищать стек будущего, иначе мы потеряем цепочку для кнопки "Вперед"
                await PlayTrack(previousTrack, addToHistory: false, clearForward: false);
            }
            catch (Exception ex)
            {
                _mainWindow.BottomTrackTitle.Text = $"Ошибка назад: {ex.Message}";
            }
            finally
            {
                //BtnPrev.IsEnabled = true;
            }
        }

        private bool _isSkipping = false;
        public async Task PlayNextTrackAsync(SearchResultDto results)
        {


            if (_isSkipping) return;
            _isSkipping = true;


            try
            {
                // ПРИОРИТЕТ 1: история вперёд
                if (_forwardStack.Count > 0)
                {
                    var nextFromHistory = _forwardStack.Pop();

                    if (nextFromHistory != null && results.Tracks != null)
                    {
                        foreach (var track in results.Tracks)
                        {
                            // Сверяем название и автора (как в твоем методе клика)
                            bool isMatch = string.Equals(track.Title?.Trim(), nextFromHistory.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                        && string.Equals(track.Author?.Trim(), nextFromHistory.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                            if (isMatch)
                            {
                                // Нашли! Меняем флаг в "сырых" данных до инициализации UI
                                track.IsPlaying = true;
                                break; // Выходим из цикла
                            }
                        }
                    }


                    await PlayTrack(nextFromHistory, addToHistory: true, clearForward: false);
                    return;
                }

                // ПРИОРИТЕТ 2: очередь рекомендаций
                if (_playbackQueue.TryDequeue(out var next))
                {
                    _mediaPlayer.Stop();
                    PlayerStatusChanged?.Invoke(false);
                    
                    await _mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _mainWindow.BottomTrackTitle.Text = $"{next.Artist} - {next.Title}";
                        _mainWindow.BottomTrackTitle.Text = next.IsResolved ? "now playing" : "buffering...";

                        if (FullPlayerPage != null)
                        {
                            FullPlayerPage.BIG_TrackTitle.Text = $"{next.Title}";
                            FullPlayerPage.BIG_Author.Text = $"{next.Artist}";

                            FullPlayerPage.BIG_TrackTitle.Text = next.IsResolved ? "now playing" : "buffering...";
                        }
                    });

                    if (!next.IsResolved)
                    {
                        //TxtStatus.Text = "buffering...";
                        var cts = new CancellationTokenSource(10000);
                        while (!next.IsResolved && !cts.Token.IsCancellationRequested) // ← был баг тут, ! пропущен
                        {
                            await Task.Delay(200);
                        }
                    }

                    await PlayTrack(next, clearForward: false);
                    return;
                }

                // ПРИОРИТЕТ 3: очередь пуста, экстренный поиск
                /// ПРИОРИТЕТ 3: очередь пуста, экстренный поиск
                // ПРИОРИТЕТ 3: ждём пока очередь наполнится
                if (_currentlyPlayingTrack != null)
                {
                    _mainWindow.BottomTrackTitle.Text = "Ждём очередь...";
                    if (FullPlayerPage != null)
                    {
                        FullPlayerPage.BIG_TrackTitle.Text = "Ждём очередь...";
                    }
                    _mediaPlayer.Stop();
                    PlayerStatusChanged?.Invoke(false);
                    var cts = new CancellationTokenSource(30000); // ждём максимум 30 сек
                    while (_playbackQueue.Count == 0 && !cts.Token.IsCancellationRequested)
                        await Task.Delay(200);

                    if (_playbackQueue.TryDequeue(out var waited))
                    {


                        await _mainWindow.Dispatcher.InvokeAsync(() =>
                        {
                            _mainWindow.BottomTrackTitle.Text = $"{waited.Artist} - {waited.Title}";
                            _mainWindow.BottomTrackTitle.Text = waited.IsResolved ? "now playing" : "buffering...";
                            if (FullPlayerPage != null)
                            {
                                FullPlayerPage.BIG_TrackTitle.Text = $"{waited.Title}";
                                FullPlayerPage.BIG_Author.Text = $"{waited.Artist}";

                                FullPlayerPage.BIG_TrackTitle.Text = waited.IsResolved ? "now playing" : "buffering...";
                            }

                            if (!string.IsNullOrEmpty(waited.ImageUrl))
                            {
                                _mainWindow.BottomTrackImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(waited.ImageUrl));
                                if (FullPlayerPage != null)
                                {
                                    FullPlayerPage.BIG_TrackImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(waited.ImageUrl));
                                }
                            }
                            else
                            {
                                if (FullPlayerPage != null)
                                {
                                    FullPlayerPage.BIG_TrackImage.Source = _mainWindow.BottomTrackImage.Source;
                                }
                            }
                        });

                        if (!waited.IsResolved)
                        {
                            var resolveCts = new CancellationTokenSource(30000);
                            while (!waited.IsResolved && !resolveCts.Token.IsCancellationRequested)
                                await Task.Delay(200);
                        }

                        await PlayTrack(waited, clearForward: false);
                    }
                    else
                    {
                        _mainWindow.BottomTrackTitle.Text = "Треки не найдены.";
                    }
                }
            }
            finally
            {
                _isSkipping = false;
            }
        }






        public async void BtnNext_Click(object sender, RoutedEventArgs e, SearchResultDto results)
        {

            var currentPlaying = _currentlyPlayingTrack;
            if (currentPlaying != null && results.Tracks != null)
            {
                foreach (var track in results.Tracks)
                {
                    // Сверяем название и автора (как в твоем методе клика)
                    bool isMatch = string.Equals(track.Title?.Trim(), currentPlaying.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                && string.Equals(track.Author?.Trim(), currentPlaying.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                    if (isMatch)
                    {
                        // Нашли! Меняем флаг в "сырых" данных до инициализации UI
                        track.IsPlaying = false;
                        break; // Выходим из цикла
                    }
                }
            }
            try
            {
                //BtnNext.IsEnabled = false;
                await PlayNextTrackAsync(results);
            }
            catch (Exception ex)
            {
                _mainWindow.BottomTrackTitle.Text = $"Ошибка: {ex.Message}";
            }
            finally
            {
                //BtnNext.IsEnabled = true;
            }
        }


        public void TimelineSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _isDragging = true;

        }

        public async void TimelineSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _mediaPlayer.Time = (long)(_mainWindow.TimelineSlider.Value * 1000);
            //_mediaPlayer.Time = (long)(FullPlayerPage.BIG_Slider.Value * 1000);
            // обратно в миллисекунды
            _isDragging = false;
        }

        public async void TimelineSlider_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 1. Проверяем, что кликнули именно по слайдеру, а не по самой пипке (Thumb)
            // Если кликнуть по Thumb, сработает стандартный Drag, и нам мешать ему не нужно
            if (e.OriginalSource is System.Windows.Controls.Primitives.Thumb)
                return;

            var slider = (System.Windows.Controls.Slider)sender;

            // 2. Получаем позицию клика относительно самого слайдера
            System.Windows.Point clickPoint = e.GetPosition(slider);

            // 3. Вычисляем процент сдвига (от 0.0 до 1.0) в зависимости от ширины слайдера
            double relativePosition = clickPoint.X / slider.ActualWidth;

            // Ограничиваем рамками от 0 до 1 на всякий случай
            relativePosition = Math.Max(0.0, Math.Min(1.0, relativePosition));

            // 4. Переводим процент в реальное значение слайдера (время трека)
            double newValue = slider.Minimum + (relativePosition * (slider.Maximum - slider.Minimum));

            // 5. Заставляем UI временно замереть, как при перетаскивании
            _isDragging = true;
            _mediaPlayer.Time = (long)(newValue * 1000);
            _isDragging = false;

            // 8. Возвращаем всё назад
            _isDragging = false;
        }
    }
}
