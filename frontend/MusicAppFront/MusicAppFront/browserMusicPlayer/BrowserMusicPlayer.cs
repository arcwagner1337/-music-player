using Microsoft.Web.WebView2;
using MusicAppFront.Views.Windows;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text;
using System.Threading;
using System.Threading;
using System.Threading.Tasks;

using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Threading;
using static System.Net.Mime.MediaTypeNames;

namespace MusicAppFront.browserMusicPlayer
{
    public class BrowserMusicPlayer
    {

        private int _pushStream;
        private int _stream;
        private HttpClient _client = new HttpClient();
        private DispatcherTimer _timer;
        private bool _isDragging = false;
        private CancellationTokenSource _cts;
        public bool _isPlayerReady = false;
        private bool _isHttpLoading = false;
        private System.Windows.Threading.DispatcherTimer _syncTimer;
        private List<string> _globalHistory = new List<string>();

        private string _currentArtist = "";
        private string _currentTrack = "";


        private Queue<TrackWithStreamDto> _playbackQueue = new Queue<TrackWithStreamDto>();

        // Стек истории (те, что уже проиграли, для кнопки Prev)
        private Stack<TrackWithStreamDto> _historyStack = new Stack<TrackWithStreamDto>();
        private Stack<TrackWithStreamDto> _forwardStack = new Stack<TrackWithStreamDto>();

        // То, что играет прямо сейчас
        internal TrackWithStreamDto _currentlyPlayingTrack;

        // Токен для отмены фоновых задач (чтобы старые запросы не забивали канал)
        private CancellationTokenSource _preloadCts;
        private readonly MainWindow _mainWindow;

        // DTO-контейнер, объединяющий инфо о треке и его прямую ссылку
        public class TrackWithStreamDto
        {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string ImageUrl { get; set; }
            public string StreamUrl { get; set; }
            public double Duration { get; set; }
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

        public bool _isPlaying = false;

        public BrowserMusicPlayer(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        private async Task PreloadRecommendationsAsync(string artist, string track, CancellationToken token)
        {
            try
            {
                if (token.IsCancellationRequested) return;

                if (_playbackQueue.Count >= 3) return;

                // Добавляем текущий трек в глобальную историю (чтобы он не выпал в рекомендациях)
                string currentTrackId = $"{artist.ToLower()} - {track.ToLower()}";
                if (!_globalHistory.Contains(currentTrackId)) _globalHistory.Add(currentTrackId);

                // Собираем параметры exclude для URL
                // Берем только последние 40 треков из истории, чтобы URL не стал слишком длинным
                var excludeParams = string.Join("", _globalHistory
    .Skip(Math.Max(0, _globalHistory.Count - 40)) // Пропускаем всё, кроме последних 40
    .Select(x => $"&exclude={Uri.EscapeDataString(x)}"));

                string url = $"https://localhost:7296/api/music/GetNextRecommended?artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(track)}{excludeParams}";

                string json = await _client.GetStringAsync(url, token);
                Console.WriteLine(json);
                if (token.IsCancellationRequested) return;

                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                Console.WriteLine(data);

                if (data != null)
                {
                    if (token.IsCancellationRequested) return;

                    double.TryParse((string)data.duration, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dur);

                    var nextTrack = new TrackWithStreamDto
                    {
                        Artist = data.artist,
                        Title = data.title,
                        ImageUrl = data.imageUrl,
                        StreamUrl = data.streamUrl,
                        Duration = dur
                    };

                    _playbackQueue.Enqueue(nextTrack);

                    //// Если в очереди всё еще мало треков, можно вызвать рекурсивно для следующего
                    //if (_playbackQueue.Count < 2)
                    //{
                    //    _ = PreloadRecommendationsAsync(nextTrack.Artist, nextTrack.Title, token);
                    //}
                }
            }
            catch (OperationCanceledException)
            {
                // Это нормальное поведение, когда мы отменили токен при переключении трека — просто игнорируем
            }

            catch (Exception ex)
            {
                Console.WriteLine("Ошибка предзагрузки: " + ex.Message);
            }
        }


        public async Task PlayTrack(TrackWithStreamDto track, bool addToHistory = true, bool clearForward = true)
        {
            if (track == null) return;

            // 1. Управление историей прошлого
            if (addToHistory && _currentlyPlayingTrack != null)
                _historyStack.Push(_currentlyPlayingTrack);

            // 2. Управление историей будущего (НОВАЯ ЛОГИКА)
            if (clearForward)
                _forwardStack.Clear(); // Если включили новый трек вручную, "будущее" сбрасывается

            _playbackQueue.Clear();

            _currentlyPlayingTrack = track;

            // 3. Обновляем UI
            //trackName.Text = $"{track.Artist} - {track.Title}";
            _mainWindow.TimelineSlider.Maximum = track.Duration;
            _mainWindow.TimelineSlider.Value = 0;

            // 4. Предзагрузка
            _preloadCts?.Cancel();
            _preloadCts = new CancellationTokenSource();
            var currentToken = _preloadCts.Token;

            _ = PreloadRecommendationsAsync(track.Artist, track.Title, currentToken);

            // 5. Запуск видео
            await _mainWindow.HiddenBrowser.EnsureCoreWebView2Async();
            _mainWindow.HiddenBrowser.CoreWebView2.Navigate(track.StreamUrl);
        }



        public async void InitBrowser()
        {
            var options = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, null, options);
            await _mainWindow.HiddenBrowser.EnsureCoreWebView2Async(env);

            _mainWindow.HiddenBrowser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;

            // 2. Автоматически жмем "Уйти" (Confirm) на любые js-предупреждения, включая beforeunload
            _mainWindow.HiddenBrowser.CoreWebView2.ScriptDialogOpening += (s, args) =>
            {
                args.Accept(); // Принудительно жмет "ОК" / "Уйти" на любые алерты
            };

            _mainWindow.HiddenBrowser.CoreWebView2.AddWebResourceRequestedFilter("*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Media);
            _mainWindow.HiddenBrowser.CoreWebView2.WebResourceRequested += (s, args) => {
                args.Request.Headers.SetHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            };

            // Подписываемся на сообщения из браузера в C#
            _mainWindow.HiddenBrowser.WebMessageReceived += HiddenBrowser_WebMessageReceived;

            // Подписываемся на событие, когда страница медиапотока создана в WebView2
            _mainWindow.HiddenBrowser.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;

            _mainWindow.HiddenBrowser.CoreWebView2.NavigationStarting += (s, args) => {
                // Разрешаем переход, только если URL задан нами (через Navigate)
                // Либо если это самый первый старт.
                // Если YouTube пытается сам редиректнуть на другое видео (watch?v=...) - отменяем.
                if (args.IsUserInitiated == false && !args.Uri.Contains("your_trusted_stream_logic"))
                {
                    // Если это не прямая ссылка на поток, которую мы скормили - блокируем
                    args.Cancel = true; 
                }
            };

            _isPlayerReady = true;
            //TxtStatus.Text = "Плеер готов. Нажмите Play";
            //BtnPlay.IsEnabled = true;

            _syncTimer = new System.Windows.Threading.DispatcherTimer();
            _syncTimer.Interval = TimeSpan.FromMilliseconds(250); // Опрашиваем плеер 4 раза в секунду
            _syncTimer.Tick += SyncTimer_Tick;
            _syncTimer.Start();
        }


        private async void SyncTimer_Tick(object sender, EventArgs e)
        {
            if (!_isPlayerReady) return;

            string jsQuery = @"
(function() {
    var v = document.querySelector('video');
    if (!v) return 'null';
    return v.paused + ';' + v.currentTime + ';' + v.readyState + ';' + v.ended;
})()";

            string resultStr = await _mainWindow.HiddenBrowser.ExecuteScriptAsync(jsQuery);
            if (string.IsNullOrEmpty(resultStr) || resultStr == "null" || resultStr == "\"null\"") return;

            try
            {
                string cleanData = resultStr.Trim('"');
                string[] parts = cleanData.Split(';');
                if (parts.Length < 4) return;

                bool isPaused = parts[0] == "true";
                double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double currentTime);
                int.TryParse(parts[2], out int readyState);
                bool isEnded = parts[3] == "true";

                // ТРЕК ОТЫГРАЛ: Асинхронное переключение
                if (isEnded)
                {
                    _syncTimer.Stop(); // Замораживаем таймер, пока идет переключение

                    // Сбрасываем старое видео, чтобы оно не зацикливалось
                    await _mainWindow.HiddenBrowser.ExecuteScriptAsync("var v = document.querySelector('video'); if(v) v.src = '';");

                    await PlayNextTrackAsync(); // Ждем включения нового трека

                    _syncTimer.Start(); // Размораживаем таймер для нового трека
                    return;
                }

                // --- БЕЗОПАСНАЯ СИНХРОНИЗАЦИЯ ИНТЕРФЕЙСА ЧЕРЕЗ ДИСПАТЧЕР ---
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    var sb = _mainWindow.FindResource("RotateAnimation") as System.Windows.Media.Animation.Storyboard;

                    if (readyState < 3)
                    {
                        // Состояние: Буферизация потока
                        _mainWindow.BottomTrackTitle.Text = "Буферизация потока...";

                        // Меняем иконку кнопки на загрузку и крутим анимацию
                        _mainWindow.GlobalPlayPauseBtn.Content = "\uE10C";
                        _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);
                        sb?.Begin(_mainWindow, true);
                    }
                    else
                    {
                        _isPlaying = !isPaused;

                        // Останавливаем анимацию загрузки, так как поток готов к воспроизведению
                        sb?.Stop(_mainWindow);
                        if (_mainWindow.LoadingIcon != null) _mainWindow.LoadingIcon.Angle = 0;

                        if (_isPlaying)
                        {
                            // Состояние: Сейчас играет
                            _mainWindow.GlobalPlayPauseBtn.Content = "\uE103"; // Две палочки (Пауза)
                            _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);

                            // Если текст был "Буферизация...", возвращаем имя трека
                            if (_mainWindow.BottomTrackTitle.Text == "Буферизация потока..." && _currentlyPlayingTrack != null)
                            {
                                _mainWindow.BottomTrackTitle.Text = _currentlyPlayingTrack.Title;
                            }
                        }
                        else
                        {
                            // Состояние: На паузе
                            _mainWindow.GlobalPlayPauseBtn.Content = "\uE102"; // Треугольник (Плей)
                            _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(2, 0, 0, 0); // Сдвиг для центровки
                        }
                    }

                    // Обновление таймбара и текстовых полей времени
                    if (!_isDragging)
                    {
                        if (currentTime >= _mainWindow.TimelineSlider.Minimum && currentTime <= _mainWindow.TimelineSlider.Maximum)
                        {
                            _mainWindow.TimelineSlider.Value = currentTime;
                        }

                        // Форматируем время в красивый вид (0:00) перед выводом на форму
                        _mainWindow.TotalTimeText.Text = $"{FormatTime(_mainWindow.TimelineSlider.Maximum)}";
                        _mainWindow.CurrentTimeText.Text = $"{FormatTime(_mainWindow.TimelineSlider.Value)}";
                    }
                });
            }
            catch { /* Игнорируем микро-сбои парсинга при смене страниц */ }
        }





        private async void CoreWebView2_DOMContentLoaded(object sender, Microsoft.Web.WebView2.Core.CoreWebView2DOMContentLoadedEventArgs e)
        {
            // Безопасно душим качество в 144p и скрываем видео через opacity
            string injectJs = @"
        var player = document.querySelector('.html5-video-player') || document.querySelector('.video-player');
        if (player && typeof player.setPlaybackQuality === 'function') {
            player.setPlaybackQuality('tiny');
        }
        var style = document.createElement('style');
        style.innerHTML = 'video { opacity: 0.001 !important; } #masthead-container, #page-manager, .ytp-chrome-bottom { display: none !important; }';
        document.head.appendChild(style);
var checkEndedInterval = setInterval(function() {
    var v = document.querySelector('video');
    if (v && v.ended) {
        v.pause(); // Принудительно гасим плеер, не давая YouTube запустить редирект
    }
}, 500);"




;
            await _mainWindow.HiddenBrowser.ExecuteScriptAsync(injectJs);
        }


        public void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Пока тянем мышкой, C# сам обновляет текст на основе положения пипки
            if (_isDragging && _mainWindow.TotalTimeText != null & _mainWindow.CurrentTimeText != null)
            {
                

                _mainWindow.TotalTimeText.Text = $"{FormatTime(_mainWindow.TimelineSlider.Maximum)}";
                _mainWindow.CurrentTimeText.Text = $"{FormatTime(_mainWindow.TimelineSlider.Value)}";
            }
        }



        private void HiddenBrowser_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            string message = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(message)) return;

            // ОБРАБОТКА СТАРТА
            if (message == "track_started")
            {
                _isPlaying = true;
                _isHttpLoading = false; // Сбрасываем статус загрузки API
                _mainWindow.Dispatcher.Invoke(() => {
                    // 1. Находим твою анимацию в ресурсах окна и останавливаем её
                    var sb = _mainWindow.FindResource("RotateAnimation") as System.Windows.Media.Animation.Storyboard;
                    sb?.Stop(_mainWindow);

                    // 2. Сбрасываем угол вращения обратно в ноль
                    if (_mainWindow.LoadingIcon != null) _mainWindow.LoadingIcon.Angle = 0;

                    // 3. Ставим иконку паузы (две палочки)
                    _mainWindow.GlobalPlayPauseBtn.Content = "\uE103";
                    _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);
                });
            }
            // ОБРАБОТКА ПАУЗЫ
            else if (message == "track_paused")
            {
                _isPlaying = false;
                _mainWindow.Dispatcher.Invoke(() => {
                    // 1. Тоже останавливаем анимацию, если она вдруг крутилась
                    var sb = _mainWindow.FindResource("RotateAnimation") as System.Windows.Media.Animation.Storyboard;
                    sb?.Stop(_mainWindow);
                    if (_mainWindow.LoadingIcon != null) _mainWindow.LoadingIcon.Angle = 0;

                    // 2. Ставим иконку плей (треугольник)
                    _mainWindow.GlobalPlayPauseBtn.Content = "\uE102";
                    _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(2, 0, 0, 0); // Сдвиг для центровки
                });
            }
            // ОБРАБОТКА ОКОНЧАНИЯ ПЕРЕМОТКИ
            else if (message == "track_seeked")
            {
                //Dispatcher.Invoke(() => {
                //    if (_isPlaying)
                //    {
                //        BtnPlay.Content = "⏸ Pause";
                //        TxtStatus.Text = "Сейчас играет";
                //        BtnPlay.IsEnabled = true;
                //    }
                //    else
                //    {
                //        BtnPlay.Content = "▶ Play";
                //        TxtStatus.Text = "На паузе";
                //        BtnPlay.IsEnabled = true;
                //    }
                //});
            }

            else if (message == "track_buffering")
            {
                _mainWindow.Dispatcher.Invoke(() => {
                    // 1. Выводим текст статуса в название трека, как ты просил
                    _mainWindow.BottomTrackTitle.Text = "Буферизация потока...";

                    // 2. Меняем иконку внутри кнопки на кольцо загрузки (или шестеренку)
                    _mainWindow.GlobalPlayPauseBtn.Content = "\uE10C";
                    _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);

                    // 3. Находим твой Storyboard в ресурсах и запускаем его
                    var sb = _mainWindow.FindResource("RotateAnimation") as System.Windows.Media.Animation.Storyboard;
                    sb?.Begin(_mainWindow, true); // true позволяет управлять анимацией (останавливать её позже)
                });
            }
            // ОБРАБОТКА ДВИЖЕНИЯ ПОЛЗУНКА
            else if (message.StartsWith("time:"))
            {
                // Если пользователь СЕЙЧАС тянет ползунок мышкой — игнорируем тики из браузера
                if (_isDragging) return;

                string timeStr = message.Replace("time:", "");
                if (double.TryParse(timeStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double pos))
                {
                    _mainWindow.Dispatcher.Invoke(() => {
                        // Принудительно обновляем ползунок и текст, не завязываясь на флаг _isPlaying
                        if (pos >= _mainWindow.TimelineSlider.Minimum && pos <= _mainWindow.TimelineSlider.Maximum)
                        {
                            _mainWindow.TimelineSlider.Value = pos;
                        }
                        
                        _mainWindow.TotalTimeText.Text = $"{FormatTime(_mainWindow.TimelineSlider.Maximum)}";
                        _mainWindow.CurrentTimeText.Text = $"{FormatTime(_mainWindow.TimelineSlider.Value)}";
                    });
                }
            }
        }


        //private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        //{
        //    if (!_isPlayerReady) return;

        //    // Проверяем, загружено ли уже видео (пауза/плей)
        //    string currentSrc = await _mainWindow.HiddenBrowser.ExecuteScriptAsync("document.querySelector('video') ? document.querySelector('video').currentSrc : ''");
        //    if (!string.IsNullOrEmpty(currentSrc) && currentSrc != "null" && currentSrc != "\"\"")
        //    {
        //        await _mainWindow.HiddenBrowser.ExecuteScriptAsync(_isPlaying ? "document.querySelector('video').pause();" : "document.querySelector('video').play();");
        //        return;
        //    }

        //    // Если ничего не играет — ищем первый трек (например, дефолтный)
        //    try
        //    {
        //        string artist = "ratt", track = "round and round";
        //        TxtStatus.Text = "Поиск...";
        //        BtnPlay.IsEnabled = true;

        //        string url = $"https://localhost:7296/api/music/stream?artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(track)}";
        //        string json = await _client.GetStringAsync(url);

        //        // Десериализуем массив строк: [0] - url, [1] - duration
        //        var data = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(json);

        //        if (data != null && data.Count >= 2)
        //        {
        //            double.TryParse(data[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double duration);
        //            var firstTrack = new TrackWithStreamDto
        //            {
        //                Artist = artist,
        //                Title = track,
        //                StreamUrl = data[0],
        //                Duration = duration
        //            };


        //            await PlayTrack(firstTrack);
        //        }
        //    }
        //    catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        //}









        //private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        //{
        //    if (!_isPlayerReady) return;

        //    string currentSrc = await HiddenBrowser.ExecuteScriptAsync("document.querySelector('video') ? document.querySelector('video').currentSrc : ''");
        //    currentSrc = currentSrc.Trim('"');

        //    // Логика управления, если трек уже загружен
        //    if (!string.IsNullOrEmpty(currentSrc) && currentSrc != "null")
        //    {
        //        // Защита от спама кнопкой: меняем текст только временно
        //
        //        .Text = "Обработка...";

        //        if (_isPlaying)
        //        {
        //            await HiddenBrowser.ExecuteScriptAsync("document.querySelector('video').pause();");
        //        }
        //        else
        //        {
        //            await HiddenBrowser.ExecuteScriptAsync("document.querySelector('video').play();");
        //        }
        //        return;
        //    }

        //    // САМЫЙ ПЕРВЫЙ ЗАПУСК:
        //    BtnPlay.Content = "⌛ Загрузка...";
        //    TxtStatus.Text = "Поиск на сервере...";

        //    try
        //    {
        //        string artist = "dokken";
        //        string track = "in my dreams";

        //        string encodedArtist = Uri.EscapeDataString(artist);
        //        string encodedTrack = Uri.EscapeDataString(track);
        //        List<string> data = new List<string>();
        //        string requestUrl = $"https://localhost:7296/api/music/stream?artist={encodedArtist}&track={encodedTrack}";

        //        string jsonString = await _client.GetStringAsync(requestUrl);

        //        string cleanResult = jsonString.Trim('[', ']', ' ', '\n', '\r');

        //        // 3. Режем массив по центральному стыку
        //        string[] dataParts = cleanResult.Split(new string[] { "\",\"" }, StringSplitOptions.None);

        //        Console.WriteLine("url  " + dataParts[0].Trim('"'));
        //        Console.WriteLine("time  " + dataParts[1].Trim('"'));
        //        Console.WriteLine(cleanResult);



        //        //string videoUrl = jsonString.Trim(' ', '"', '\n', '\r');
        //        string videoUrl = dataParts[0].Trim('"');

        //        string durStr = dataParts[1].Trim('"');

        //        double trackDuration = 0;
        //        if (double.TryParse(durStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out trackDuration))
        //        {
        //            TimelineSlider.Minimum = 0;
        //            TimelineSlider.Maximum = trackDuration > 0 ? trackDuration : 100;
        //            TimelineSlider.Value = 0;

        //            if (double.TryParse(durStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out trackDuration))
        //            {
        //                TimelineSlider.Minimum = 0;
        //                TimelineSlider.Maximum = trackDuration > 0 ? trackDuration : 100;
        //                TimelineSlider.Value = 0;

        //                // РАСКОММЕНТИРОВАНО: Задаем начальный текст счетчика времени
        //                TxtTime.Text = $"00:00 / {FormatTime(trackDuration)}";
        //            }
        //        }

        //        if (!string.IsNullOrEmpty(videoUrl) && videoUrl.StartsWith("http"))
        //        {
        //            TxtStatus.Text = "Буферизация потока...";
        //            HiddenBrowser.CoreWebView2.Navigate(videoUrl);
        //        }
        //        else
        //        {
        //            MessageBox.Show($"Сервер вернул некорректный URL: {videoUrl}");
        //            BtnPlay.Content = "▶ Play";
        //        }

        //        _currentArtist = artist;
        //        _currentTrack = track;
        //        _playbackQueue.Clear(); // Очищаем старый хвост очереди
        //        _preloadCts?.Cancel();
        //        _preloadCts = new System.Threading.CancellationTokenSource();

        //        // Запускаем наполнение обоймы на следующие 10 треков
        //        _ = PreloadRecommendationsAsync(_currentArtist, _currentTrack, _preloadCts.Token);

        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Ошибка загрузки: {ex.Message}");
        //        BtnPlay.Content = "▶ Play";
        //    }
        //}


        //private void StartSliderTimer()
        //{
        //    if (_timer == null)
        //    {
        //        _timer = new DispatcherTimer();
        //        _timer.Interval = TimeSpan.FromMilliseconds(250);
        //        _timer.Tick += async (s, e) =>
        //        {
        //            // Двигаем ползунок только если видео реально играет и пользователь его не тянет мышкой
        //            if (!_isDragging && _isPlaying)
        //            {
        //                string currentPos = await _mainWindow.HiddenBrowser.ExecuteScriptAsync("document.querySelector('video') ? document.querySelector('video').currentTime : '0'");
        //                currentPos = currentPos.Trim('"');
        //                if (double.TryParse(currentPos, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double pos))
        //                {
        //                    TimelineSlider.Value = pos;
        //                }
        //            }
        //        };
        //    }
        //    _timer.Start();
        //}


        private string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                return "00:00";

            TimeSpan t = TimeSpan.FromSeconds(seconds);

            // Если трек идет дольше часа, выводим часы, иначе просто Минуты:Секунды
            return t.TotalHours >= 1
                ? t.ToString(@"hh\:mm\:ss")
                : t.ToString(@"mm\:ss");
        }







        public async void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_historyStack.Count == 0)
            {
                //TxtStatus.Text = "Это самый первый трек";
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




                // Играем его. 
                // false -> не добавлять в историю прошлого повторно (мы его оттуда только что взяли)
                // false -> НЕ очищать стек будущего, иначе мы потеряем цепочку для кнопки "Вперед"
                await PlayTrack(previousTrack, addToHistory: false, clearForward: false);
            }
            catch (Exception ex)
            {
                //TxtStatus.Text = $"Ошибка назад: {ex.Message}";
            }
            finally
            {
                //BtnPrev.IsEnabled = true;
            }
        }


        private async Task PlayNextTrackAsync()
        {
            // ПРИОРИТЕТ 1: Если пользователь кликал "Назад", возвращаем его по истории "Вперед"
            if (_forwardStack.Count > 0)
            {
                var nextFromHistory = _forwardStack.Pop();

                


                // true -> добавляем текущий трек в историю прошлого
                // false -> НЕ очищаем стек будущего, так как мы сами из него только что взяли элемент
                await PlayTrack(nextFromHistory, addToHistory: true, clearForward: false);
                return;
            }

            // ПРИОРИТЕТ 2: Если истории будущего нет, смотрим заготовленную очередь рекомендаций
            if (_playbackQueue.Count > 0)
            {
                var next = _playbackQueue.Dequeue();


                await PlayTrack(next);
                return;
            }

            // ПРИОРИТЕТ 3: Если и очередь пуста, экстренно ищем новые рекомендации по сети
            if (_currentlyPlayingTrack != null)
            {
                _mainWindow.BottomTrackTitle.Text = "Подбираем следующий трек...";

                await PreloadRecommendationsAsync(_currentlyPlayingTrack.Artist, _currentlyPlayingTrack.Title, CancellationToken.None);

                if (_playbackQueue.Count > 0)
                {
                    await PlayTrack(_playbackQueue.Dequeue());
                }
                else
                {
                    _mainWindow.BottomTrackTitle.Text = "Очередь пуста. Треки не найдены.";
                }
            }
        }



        public async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //BtnNext.IsEnabled = false;
                await PlayNextTrackAsync();
            }
            catch (Exception ex)
            {
                //TxtStatus.Text = $"Ошибка: {ex.Message}";
            }
            finally
            {
                //BtnNext.IsEnabled = true;
            }
        }


        public void TimelineSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _isDragging = true;
            // Говорим браузеру: "Заткнись и не шли время, пока я тащу слайдер"
            _mainWindow.HiddenBrowser.ExecuteScriptAsync("window.isSliderDragging = true;");
        }

        public async void TimelineSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            double newValue = _mainWindow.TimelineSlider.Value;

            // УБРАЛИ ВСЕ СМЕНЫ ТЕКСТА И СТАТУСОВ, чтобы UI не моргал и не дёргался!

            // 1. Сразу отправляем новую позицию времени в браузер
            string jsSetTime = $"var v = document.querySelector('video'); if(v) {{ v.currentTime = {newValue.ToString(System.Globalization.CultureInfo.InvariantCulture)}; }}";
            await _mainWindow.HiddenBrowser.ExecuteScriptAsync(jsSetTime);

            // 2. Снимаем флаг блокировки, чтобы C# снова начал принимать время из Chromium
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
            _mainWindow.HiddenBrowser.ExecuteScriptAsync("window.isSliderDragging = true;");

            // 6. Присваиваем новое значение слайдеру
            slider.Value = newValue;

            // 7. Отправляем время в Chromium (используем инвариантную культуру для точки вместо запятой)
            string jsSetTime = $"var v = document.querySelector('video'); if(v) {{ v.currentTime = {newValue.ToString(System.Globalization.CultureInfo.InvariantCulture)}; }}";
            await _mainWindow.HiddenBrowser.ExecuteScriptAsync(jsSetTime);

            // 8. Возвращаем всё назад
            _isDragging = false;
        }








        //private async void InitBrowser()
        //{
        //    await HiddenBrowser.EnsureCoreWebView2Async();

        //    // Добавляем обработчик события 'loadedmetadata', чтобы автоматически ставить Maximum у слайдера
        //    string html = @"
        //<html>
        //<body>
        //    <audio id='player' autoplay></audio>
        //    <script>
        //        var player = document.getElementById('player');
        //        player.onloadedmetadata = () => {
        //            // Отправляем длительность в C# через заголовок окна или просто ждем запроса
        //            window.chrome.webview.postMessage({ type: 'duration', value: player.duration });
        //        };
        //    </script>
        //</body>
        //</html>";

        //    HiddenBrowser.NavigateToString(html);


    }
}
