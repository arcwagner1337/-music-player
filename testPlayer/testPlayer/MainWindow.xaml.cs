using ManagedBass;
//using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
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
using Microsoft.Web.WebView2;
using Newtonsoft.Json;



namespace testPlayer
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        private int _pushStream;
        private int _stream;
        private HttpClient _client = new HttpClient();
        private DispatcherTimer _timer;
        private bool _isDragging = false;
        private CancellationTokenSource _cts;
        private bool _isPlayerReady = false;
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
        private TrackWithStreamDto _currentlyPlayingTrack;

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
        public MainWindow()
        {
            InitializeComponent();
            InitBrowser();

        }

        private async Task PreloadRecommendationsAsync(string artist, string track, CancellationToken token)
        {
            try
            {
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

                string json = await _client.GetStringAsync(url);
                Console.WriteLine(json);
                if (token.IsCancellationRequested) return;

                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                Console.WriteLine(data);

                if (data != null)
                {
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

                    // Если в очереди всё еще мало треков, можно вызвать рекурсивно для следующего
                    if (_playbackQueue.Count < 2)
                    {
                        _ = PreloadRecommendationsAsync(nextTrack.Artist, nextTrack.Title, token);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка предзагрузки: " + ex.Message);
            }
        }


        private async Task PlayTrack(TrackWithStreamDto track, bool addToHistory = true, bool clearForward = true)
        {
            if (track == null) return;

            // 1. Управление историей прошлого
            if (addToHistory && _currentlyPlayingTrack != null)
                _historyStack.Push(_currentlyPlayingTrack);

            // 2. Управление историей будущего (НОВАЯ ЛОГИКА)
            if (clearForward)
                _forwardStack.Clear(); // Если включили новый трек вручную, "будущее" сбрасывается

            _currentlyPlayingTrack = track;

            // 3. Обновляем UI
            TxtStatus.Text = $"{track.Artist} - {track.Title}";
            TimelineSlider.Maximum = track.Duration;
            TimelineSlider.Value = 0;

            // 4. Предзагрузка
            _preloadCts?.Cancel();
            _preloadCts = new CancellationTokenSource();
            var currentToken = _preloadCts.Token;

            _ = PreloadRecommendationsAsync(track.Artist, track.Title, currentToken);

            // 5. Запуск видео
            await HiddenBrowser.EnsureCoreWebView2Async();
            HiddenBrowser.CoreWebView2.Navigate(track.StreamUrl);
        }



        private async void InitBrowser()
        {
            var options = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, null, options);
            await HiddenBrowser.EnsureCoreWebView2Async(env);

            HiddenBrowser.CoreWebView2.AddWebResourceRequestedFilter("*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.Media);
            HiddenBrowser.CoreWebView2.WebResourceRequested += (s, args) => {
                args.Request.Headers.SetHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            };

            // Подписываемся на сообщения из браузера в C#
            HiddenBrowser.WebMessageReceived += HiddenBrowser_WebMessageReceived;

            // Подписываемся на событие, когда страница медиапотока создана в WebView2
            HiddenBrowser.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;

            HiddenBrowser.CoreWebView2.NavigationStarting += (s, args) => {
                // Разрешаем переход, только если URL задан нами (через Navigate)
                // Либо если это самый первый старт.
                // Если YouTube пытается сам редиректнуть на другое видео (watch?v=...) - отменяем.
                if (args.IsUserInitiated == false && !args.Uri.Contains("your_trusted_stream_logic"))
                {
                    // Если это не прямая ссылка на поток, которую мы скормили - блокируем
                    // args.Cancel = true; 
                }
            }; 

            _isPlayerReady = true;
            TxtStatus.Text = "Плеер готов. Нажмите Play";

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

            string resultStr = await HiddenBrowser.ExecuteScriptAsync(jsQuery);
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

                // ТРЕК ВЫГРАЛ: Асинхронное переключение
                if (isEnded)
                {
                    _syncTimer.Stop(); // Замораживаем таймер, пока идет переключение

                    // Сбрасываем старое видео, чтобы оно не зацикливалось
                    await HiddenBrowser.ExecuteScriptAsync("var v = document.querySelector('video'); if(v) v.src = '';");

                    await PlayNextTrackAsync(); // Ждем включения нового трека

                    _syncTimer.Start(); // Размораживаем таймер для нового трека
                    return;
                }

                // --- СИНХРОНИЗАЦИЯ ИНТЕРФЕЙСА ---
                if (readyState < 3)
                {
                    TxtStatus.Text = "Буферизация потока...";
                    BtnPlay.Content = "⌛ Загрузка...";
                }
                else
                {
                    _isPlaying = !isPaused;
                    if (_isPlaying)
                    {
                        BtnPlay.Content = "⏸ Pause";
                        TxtStatus.Text = "Сейчас играет";
                    }
                    else
                    {
                        BtnPlay.Content = "▶ Play";
                        TxtStatus.Text = "На паузе";
                    }
                }

                if (!_isDragging)
                {
                    if (currentTime >= TimelineSlider.Minimum && currentTime <= TimelineSlider.Maximum)
                    {
                        TimelineSlider.Value = currentTime;
                    }
                    TxtTime.Text = $"{FormatTime(TimelineSlider.Value)} / {FormatTime(TimelineSlider.Maximum)}";
                }
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
            await HiddenBrowser.ExecuteScriptAsync(injectJs);
        }


        private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Пока тянем мышкой, C# сам обновляет текст на основе положения пипки
            if (_isDragging && TxtTime != null)
            {
                TxtTime.Text = $"{FormatTime(TimelineSlider.Value)} / {FormatTime(TimelineSlider.Maximum)}";
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
                Dispatcher.Invoke(() => {
                    BtnPlay.Content = "⏸ Pause";
                    TxtStatus.Text = "Сейчас играет";
                });
            }
            // ОБРАБОТКА ПАУЗЫ
            else if (message == "track_paused")
            {
                _isPlaying = false;
                Dispatcher.Invoke(() => {
                    BtnPlay.Content = "▶ Play";
                    TxtStatus.Text = "На паузе";
                });
            }
            // ОБРАБОТКА ОКОНЧАНИЯ ПЕРЕМОТКИ
            else if (message == "track_seeked")
            {
                Dispatcher.Invoke(() => {
                    if (_isPlaying)
                    {
                        BtnPlay.Content = "⏸ Pause";
                        TxtStatus.Text = "Сейчас играет";
                    }
                    else
                    {
                        BtnPlay.Content = "▶ Play";
                        TxtStatus.Text = "На паузе";
                    }
                });
            }

            else if (message == "track_buffering")
            {
                Dispatcher.Invoke(() => {
                    // Меняем только статус текста, саму кнопку "Pause" не превращаем в загрузку, 
                    // чтобы у пользователя оставалась возможность нажать на нее и остановить зависший поток!
                    TxtStatus.Text = "Буферизация потока...";
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
                    Dispatcher.Invoke(() => {
                        // Принудительно обновляем ползунок и текст, не завязываясь на флаг _isPlaying
                        if (pos >= TimelineSlider.Minimum && pos <= TimelineSlider.Maximum)
                        {
                            TimelineSlider.Value = pos;
                        }
                        TxtTime.Text = $"{FormatTime(TimelineSlider.Value)} / {FormatTime(TimelineSlider.Maximum)}";
                    });
                }
            }
        }


        private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPlayerReady) return;

            // Проверяем, загружено ли уже видео (пауза/плей)
            string currentSrc = await HiddenBrowser.ExecuteScriptAsync("document.querySelector('video') ? document.querySelector('video').currentSrc : ''");
            if (!string.IsNullOrEmpty(currentSrc) && currentSrc != "null" && currentSrc != "\"\"")
            {
                await HiddenBrowser.ExecuteScriptAsync(_isPlaying ? "document.querySelector('video').pause();" : "document.querySelector('video').play();");
                return;
            }

            // Если ничего не играет — ищем первый трек (например, дефолтный)
            try
            {
                string artist = "ratt", track = "round and round";
                TxtStatus.Text = "Поиск...";

                string url = $"https://localhost:7296/api/music/stream?artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(track)}";
                string json = await _client.GetStringAsync(url);

                // Десериализуем массив строк: [0] - url, [1] - duration
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(json);

                if (data != null && data.Count >= 2)
                {
                    double.TryParse(data[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double duration);
                    var firstTrack = new TrackWithStreamDto
                    {
                        Artist = artist,
                        Title = track,
                        StreamUrl = data[0],
                        Duration = duration
                    };


                    await PlayTrack(firstTrack);
                }
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }









        //private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        //{
        //    if (!_isPlayerReady) return;

        //    string currentSrc = await HiddenBrowser.ExecuteScriptAsync("document.querySelector('video') ? document.querySelector('video').currentSrc : ''");
        //    currentSrc = currentSrc.Trim('"');

        //    // Логика управления, если трек уже загружен
        //    if (!string.IsNullOrEmpty(currentSrc) && currentSrc != "null")
        //    {
        //        // Защита от спама кнопкой: меняем текст только временно
        //        TxtStatus.Text = "Обработка...";

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


        private void StartSliderTimer()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromMilliseconds(250);
                _timer.Tick += async (s, e) =>
                {
                    // Двигаем ползунок только если видео реально играет и пользователь его не тянет мышкой
                    if (!_isDragging && _isPlaying)
                    {
                        string currentPos = await HiddenBrowser.ExecuteScriptAsync("document.querySelector('video') ? document.querySelector('video').currentTime : '0'");
                        currentPos = currentPos.Trim('"');
                        if (double.TryParse(currentPos, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double pos))
                        {
                            TimelineSlider.Value = pos;
                        }
                    }
                };
            }
            _timer.Start();
        }


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







        private async void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_historyStack.Count == 0)
            {
                TxtStatus.Text = "Это самый первый трек";
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
                TxtStatus.Text = $"Ошибка назад: {ex.Message}";
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
                TxtStatus.Text = "Подбираем следующий трек...";

                await PreloadRecommendationsAsync(_currentlyPlayingTrack.Artist, _currentlyPlayingTrack.Title, CancellationToken.None);

                if (_playbackQueue.Count > 0)
                {
                    await PlayTrack(_playbackQueue.Dequeue());
                }
                else
                {
                    TxtStatus.Text = "Очередь пуста. Треки не найдены.";
                }
            }
        }



        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //BtnNext.IsEnabled = false;
                await PlayNextTrackAsync();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Ошибка: {ex.Message}";
            }
            finally
            {
                //BtnNext.IsEnabled = true;
            }
        }


        private void TimelineSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _isDragging = true;
            // Говорим браузеру: "Заткнись и не шли время, пока я тащу слайдер"
            HiddenBrowser.ExecuteScriptAsync("window.isSliderDragging = true;");
        }

        private async void TimelineSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            double newValue = TimelineSlider.Value;

            // УБРАЛИ ВСЕ СМЕНЫ ТЕКСТА И СТАТУСОВ, чтобы UI не моргал и не дёргался!

            // 1. Сразу отправляем новую позицию времени в браузер
            string jsSetTime = $"var v = document.querySelector('video'); if(v) {{ v.currentTime = {newValue.ToString(System.Globalization.CultureInfo.InvariantCulture)}; }}";
            await HiddenBrowser.ExecuteScriptAsync(jsSetTime);

            // 2. Снимаем флаг блокировки, чтобы C# снова начал принимать время из Chromium
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
        //}
        private async void PlayUrl(string url)
        {
            // Просто передаем URL в тег audio внутри браузера
            string js = $@"
            var audio = document.getElementById('player');
            audio.src = '{url}';
            audio.play();
        ";
            await HiddenBrowser.ExecuteScriptAsync(js);

            // Получаем длительность для слайдера (через секунду, когда прогрузится)
            await Task.Delay(1000);
            string durationStr = await HiddenBrowser.ExecuteScriptAsync("document.getElementById('player').duration");

            // 1. Убираем лишние кавычки, которые приходят из JS
            durationStr = durationStr.Trim('"');

            // 2. Проверяем на null и парсим (используем инвариантную культуру, чтобы не воевать с точками/запятыми)
            if (durationStr != "null" && double.TryParse(durationStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dur))
            {
                TimelineSlider.Maximum = dur;
                _timer.Start();
            }
        }

        //private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        //{
        //    string currentSrc = await HiddenBrowser.ExecuteScriptAsync("document.getElementById('player').src");
        //    currentSrc = currentSrc.Trim('"');

        //    // Если src пустой или равен "null", значит трек еще не загружали
        //    if (string.IsNullOrEmpty(currentSrc) || currentSrc == "null")
        //    {
        //        Console.WriteLine("Загружаем первый раз...");
        //        string url = "тут ссылка";
        //        PlayUrl(url);
        //        _isPlaying = true;
        //        BtnPlay.Content = "⏸ Pause";
        //        return;
        //    }

        //    if (_isPlaying)
        //    {
        //        Console.WriteLine("no asd");
        //        await HiddenBrowser.ExecuteScriptAsync("document.getElementById('player').pause();");
        //        _isPlaying = false;
        //        BtnPlay.Content = "▶ Play";
        //        _timer.Stop();
        //    }
        //    else
        //    {

        //        Console.WriteLine("???");
        //        await HiddenBrowser.ExecuteScriptAsync("document.getElementById('player').play();");
        //        _isPlaying = true;
        //        BtnPlay.Content = "⏸ Pause";
        //        _timer.Start();
        //    }

        //}

    }

    //if (Bass.Init())
    //{
    //    // Загружаем плагин. Теперь Bass.CreateStream поймет формат WebM
    //    Bass.PluginLoad("bassopus.dll");
    //    Bass.PluginLoad("bass_aac.dll");
    //    Bass.PluginLoad("bass_ssl.dll");
    //}
    //else
    //{
    //    MessageBox.Show("Ошибка инициализации BASS");
    //}

    //_timer = new DispatcherTimer();
    //_timer.Interval = TimeSpan.FromMilliseconds(200); // Обновляем 5 раз в секунду
    //_timer.Tick += (s, e) =>
    //{
    //    if (_pushStream != 0 && !_isDragging)
    //    {
    //        // Получаем позицию через твой метод GetPos
    //        TimelineSlider.Value = GetPos();
    //    }
    //};



    //private async void InitBrowser()
    //{
    //    await HiddenBrowser.EnsureCoreWebView2Async();
    //    // Загружаем пустую страницу, чтобы JS работал
    //    HiddenBrowser.NavigateToString("<html><body><audio id='player' autoplay></audio></body></html>");
    //}

    //try
    //{
    //    TxtStatus.Text = "Ищем ссылку...";

    //    // Получаем "длинную" ссылку от бэка
    //    // Убедись, что бэк возвращает ПРОСТО СТРОКУ (Content-Type: text/plain)
    //    string directUrl = await _client.GetStringAsync("https://localhost:7296/api/music/get-url?artist=judas%20priest&track=creatures");
    //    Console.WriteLine(directUrl);

    //    // Вызываем метод, который лежит в этом же классе
    //    //PlayViaPush("judas priest", "creatures");
    //    PlayUrl(directUrl);
    //}
    //catch (Exception ex)
    //{
    //    MessageBox.Show($"Ошибка сети: {ex.Message}");
    //}

    //public void PlayUrl(string url)
    //{
    //    Bass.StreamFree(_stream);

    //    // Добавляем плагин SSL (если он еще не подгружен)
    //    Bass.PluginLoad("bass_ssl.dll");
    //    Bass.PluginLoad("bassopus.dll");

    //    // Ютуб ОЧЕНЬ хочет User-Agent. Без него — тишина или ошибка.
    //    string urlWithUserAgent = url + "|User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36";

    //    // Создаем стрим напрямую. BassFlags.AsyncStream не дает окну зависнуть.
    //    // Если ManagedBass ругается на AsyncStream, используй (BassFlags)0x40
    //    _stream = Bass.CreateStream(urlWithUserAgent, 0, BassFlags.Default | (BassFlags)0x40, null);

    //    if (_stream != 0)
    //    {
    //        Bass.ChannelPlay(_stream);

    //        // У BASS есть крутая фича: он сам узнает длительность сетевого файла!
    //        double duration = Bass.ChannelBytes2Seconds(_stream, Bass.ChannelGetLength(_stream));
    //        TimelineSlider.Maximum = (duration > 0) ? duration : 300;

    //        _timer.Start();
    //        TxtStatus.Text = "Играет напрямую из YouTube!";
    //    }
    //    else
    //    {
    //        MessageBox.Show($"Ошибка BASS: {Bass.LastError}");
    //    }
    //}




    //public void SetPos(double seconds)
    //{
    //    // Перемотка в одну строку!
    //    long pos = Bass.ChannelSeconds2Bytes(_pushStream, seconds);
    //    Bass.ChannelSetPosition(_pushStream, pos);
    //}

    //public double GetPos() => Bass.ChannelBytes2Seconds(_pushStream, Bass.ChannelGetPosition(_pushStream));

    //private async void PlayViaPush(string artist, string track, int seekSeconds = 0)
    //{
    //    _cts?.Cancel();
    //    _cts = new CancellationTokenSource();
    //    var token = _cts.Token;

    //    Bass.StreamFree(_pushStream);
    //    // Создаем поток. 44100, 2 канала — СТРОГО как в FFmpeg
    //    _pushStream = Bass.CreateStream(44100, 2, BassFlags.Default, StreamProcedureType.Push);

    //    if (_pushStream == 0) return;

    //    try
    //    {
    //        var url = $"https://localhost:7296/api/music/stream?artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(track)}&seek={seekSeconds}";
    //        var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
    //        var networkStream = await response.Content.ReadAsStreamAsync();

    //        _ = Task.Run(async () => {
    //            byte[] buffer = new byte[16384]; // 16KB чанки
    //            int bytesRead;
    //            long totalBuffered = 0;
    //            bool isPlaying = false;

    //            try
    //            {
    //                while (!token.IsCancellationRequested && (bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
    //                {
    //                    // Вливаем байты в BASS. 
    //                    // Важно: передаем именно bytesRead, а не весь размер буфера!
    //                    Bass.StreamPutData(_pushStream, buffer, bytesRead);
    //                    totalBuffered += bytesRead;

    //                    // Ждем, пока наберется ~250КБ для стабильного старта
    //                    if (!isPlaying && totalBuffered > 250000)
    //                    {
    //                        Bass.ChannelPlay(_pushStream);
    //                        isPlaying = true;
    //                        Dispatcher.Invoke(() => {
    //                            _timer.Start();
    //                            BtnPlay.Content = "⏸ Pause";
    //                        });
    //                    }
    //                }
    //            }
    //            catch (OperationCanceledException) { }
    //        }, token);
    //    }
    //    catch { /* игнор ошибок отмены */ }
    //}





}



//    public partial class MainWindow : Window
//    {
//        private WaveOutEvent _outputDevice = new WaveOutEvent();
//        private RawSourceWaveStream _waveStream;
//        private HttpClient _client = new HttpClient();
//        private BufferedWaveProvider _bufferedProvider = new BufferedWaveProvider(new WaveFormat(44100, 16, 2))
//        {
//            BufferDuration = TimeSpan.FromMinutes(20),
//            ReadFully = false
//        };
//        private CancellationTokenSource _cts;

//        private DispatcherTimer _timer;
//        private bool _isDragging = false;


//        private List<(string art, string track)> _playlist = new List<(string, string)> {
//        ("Linkin Park", "Numb"),
//        ("Judas Priest", "Creatures"),
//        ("Metallica", "Enter Sandman")};

//        private int _currentTrackIndex = 0;

//        public MainWindow()
//        {
//            InitializeComponent();
//            _outputDevice.Init(_bufferedProvider);
//            _client.DefaultRequestHeaders.ConnectionClose = false;
//            _timer = new DispatcherTimer();
//            _timer.Interval = TimeSpan.FromMilliseconds(100);
//            _timer.Tick += (s, e) => {
//                if (!_isDragging && _outputDevice?.PlaybackState == PlaybackState.Playing)
//                {
//                    // В реальном проекте бери длительность из TrackDto2
//                    // Сейчас просто двигаем на 0.1 сек
//                    TimelineSlider.Value += 0.1;
//                }
//            };
//        }

//        private async void PlayCurrentTrack(int seekSeconds = 0)
//        {
//            _cts?.Cancel();
//            _cts = new CancellationTokenSource();
//            var token = _cts.Token;

//            _bufferedProvider.ClearBuffer();

//            var trackInfo = _playlist[_currentTrackIndex];
//            TxtStatus.Text = $"Загрузка: {trackInfo.art} - {trackInfo.track}...";
//            TimelineSlider.Maximum = 300; // ВАЖНО: поставь здесь реальную длительность трека!
//            TimelineSlider.Value = seekSeconds;


//            try
//            {
//                var url = $"https://localhost:7296/api/music/stream?artist={trackInfo.art}&track={trackInfo.track}&seek={seekSeconds}";
//                var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
//                var networkStream = await response.Content.ReadAsStreamAsync();

//                TimelineSlider.Value = seekSeconds;

//                //_bufferedProvider = new BufferedWaveProvider(new WaveFormat(44100, 16, 2)) { BufferDuration = TimeSpan.FromMinutes(20) };
//                //_outputDevice = new WaveOutEvent();
//                //_outputDevice.Init(_bufferedProvider);

//                _ = Task.Run(async () => {
//                    try
//                    {
//                        byte[] buffer = new byte[16384];
//                        int bytesRead;
//                        bool started = false;

//                        while (!token.IsCancellationRequested &&
//                               (bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
//                        {
//                            _bufferedProvider.AddSamples(buffer, 0, bytesRead);

//                            int bytesToStart = (seekSeconds > 0) ? 50000 : 512000;
//                            //if (!started && _bufferedProvider.BufferedBytes > 1024)
//                            if (!started && _bufferedProvider.BufferedBytes > bytesToStart)
//                            {
//                                _outputDevice.Play(); // Плеер просто начнет забирать данные из того же буфера
//                                Dispatcher.Invoke(() => { _timer.Start(); BtnPlay.Content = "⏸ Pause"; });
//                                started = true;
//                            }
//                        }
//                    }
//                    catch (OperationCanceledException)
//                    {
//                        // Это нормально, мы просто отменили задачу
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine("Ошибка загрузки: " + ex.Message);
//                    }
//                }, token);
//            }
//            catch (Exception ex) { MessageBox.Show(ex.Message); }
//        }

//        private void StopAndCleanup()
//        {
//            _cts?.Cancel(); // Сигнализируем старой задаче, что пора закругляться
//            _timer.Stop();
//            _outputDevice?.Stop();
//            _outputDevice?.Dispose();
//            _outputDevice = null;
//            // Очищаем буфер, чтобы там не оставалось «хвостов» старой песни
//            _bufferedProvider = null;
//        }

//        private void BtnPlay_Click(object sender, RoutedEventArgs e)
//        {
//            // Если устройство вывода еще не создано (первый запуск)
//            if (_outputDevice == null)
//            {
//                PlayCurrentTrack();
//                return;
//            }

//            // Если музыка сейчас играет — ставим на паузу
//            if (_outputDevice.PlaybackState == PlaybackState.Playing)
//            {
//                _outputDevice.Pause();
//                _timer.Stop();
//                BtnPlay.Content = "▶ Play"; // Меняем текст/иконку обратно
//                TxtStatus.Text = "На паузе";
//            }
//            // Если была на паузе — продолжаем
//            else if (_outputDevice.PlaybackState == PlaybackState.Paused)
//            {
//                _outputDevice.Play();
//                _timer.Start();
//                BtnPlay.Content = "⏸ Pause";
//                var trackInfo = _playlist[_currentTrackIndex];
//                TxtStatus.Text = $"Играет: {trackInfo.art}";
//            }
//            // Если была остановлена (Stopped) — запускаем заново
//            else
//            {
//                PlayCurrentTrack();
//            }
//        }





//        private async void BtnPrev_Click(object sender, RoutedEventArgs e)
//        {
//            _currentTrackIndex = (_currentTrackIndex - 1 + _playlist.Count) % _playlist.Count;
//            PlayCurrentTrack();
//        }

//        private async void BtnNext_Click(object sender, RoutedEventArgs e)
//        {
//            _currentTrackIndex = (_currentTrackIndex + 1) % _playlist.Count;
//            PlayCurrentTrack();
//        }


//        private void TimelineSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
//        {
//            _isDragging = true; // Теперь таймер перестанет дергать ползунок, пока ты его тянешь
//        }

//        private void TimelineSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
//        {
//            _isDragging = false;
//            // Когда юзер отпустил ползунок — перезапускаем поток с новой секунды
//            int seekTo = (int)TimelineSlider.Value;
//            PlayCurrentTrack(seekTo);
//        }

//    }
//}

//var response = await _client.GetAsync("https://localhost:7296/api/music/stream?artist=judas%20priest&track=cratures",
//                                      HttpCompletionOption.ResponseHeadersRead);



//private async void btnPlay_Click(object sender, RoutedEventArgs e)
//{
//    try
//    {
//        _outputDevice?.Stop();
//        _outputDevice?.Dispose();

//        // 1. Делаем запрос
//        var response = await _client.GetAsync("https://localhost:7296/api/music/stream?artist=judas%20priest&track=rage",
//                                              HttpCompletionOption.ResponseHeadersRead);

//        var networkStream = await response.Content.ReadAsStreamAsync();

//        // 2. Настраиваем буфер
//        var waveFormat = new WaveFormat(44100, 16, 2);
//        var bufferedProvider = new BufferedWaveProvider(waveFormat)
//        {
//            BufferDuration = TimeSpan.FromMinutes(20),
//            ReadFully = false
//        };

//        _outputDevice = new WaveOutEvent();
//        _outputDevice.Init(bufferedProvider);
//        _outputDevice.Play();

//        // 3. Качаем данные
//        _ = Task.Run(async () =>
//        {
//            byte[] buffer = new byte[16384];
//            int bytesRead;
//            bool isStarted = false;

//            try
//            {
//                while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
//                {
//                    bufferedProvider.AddSamples(buffer, 0, bytesRead);

//                    // Ждем, пока наберется хотя бы 500КБ (пару секунд музыки), прежде чем жать Play
//                    if (!isStarted && bufferedProvider.BufferedBytes > 512000)
//                    {
//                        _outputDevice.Play(); // Запускаем воспроизведение только когда есть что играть
//                        isStarted = true;
//                        Console.WriteLine("[DEBUG] Воспроизведение запущено!");
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[ERROR] Ошибка чтения стрима: {ex.Message}");
//            }
//        });
//    }
//    catch (Exception ex)
//    {
//        MessageBox.Show(ex.Message);
//    }
//}



