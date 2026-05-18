using MusicAppFront.browserMusicPlayer;
using MusicAppFront.Models;
using MusicAppFront.Views.Windows;
using System;


using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Policy;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
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
using static MusicAppFront.browserMusicPlayer.BrowserMusicPlayer;
using static MusicAppFront.Models.SearchResultDto;


namespace MusicAppFront.Views.Pages
{
    /// <summary>
    /// Логика взаимодействия для SearchPage.xaml
    /// </summary>
    public partial class SearchPage : Page
    {
        private BrowserMusicPlayer _browserMusicPlayer;
        private readonly MainWindow _mainWindow;
        private HttpClient _client = new HttpClient();
        private Button _lastPlayedButton;
        internal SearchResultDto.TrackDto2 _lastPlayedTrack;

        // Защита от спама кликами, пока идет тяжелый запрос к API
        private bool _isDataLoading = false;
        public SearchPage(SearchResultDto results, MainWindow mainWindow, BrowserMusicPlayer player)
        {
            InitializeComponent();
            ArtistsList.ItemsSource = results.Artists;
            TracksList.ItemsSource = results.Tracks;
            AlbumsList.ItemsSource = results.Albums;

            _mainWindow = mainWindow;
            _browserMusicPlayer = player;
            //GlobalPlayer.Init();
        }

        public async void TrackRow_Click(object sender, RoutedEventArgs e)
        {
            if (!_browserMusicPlayer._isPlayerReady) return;

            var btn = sender as Button;
            var trackData = btn?.DataContext as SearchResultDto.TrackDto2;

            if (trackData == null || btn == null) return; // Защита от NullReference

            string artist = trackData.Author;
            string track = trackData.Title;

            // 1. Проверяем, загружено ли уже видео в WebView
            string currentSrc = await _mainWindow.HiddenBrowser.ExecuteScriptAsync("document.querySelector('video') ? document.querySelector('video').currentSrc : ''");
            var currentPlaying = _browserMusicPlayer._currentlyPlayingTrack;

            // 2. ЛОГИКА ПОВТОРНОГО КЛИКА (Тыкнули на тот же самый трек, который уже играет/на паузе)
            if (!string.IsNullOrEmpty(currentSrc) && currentSrc != "null" && currentSrc != "\"\"" &&
                currentPlaying != null &&
                currentPlaying.Title.Equals(track, StringComparison.OrdinalIgnoreCase) &&
                currentPlaying.Artist.Equals(artist, StringComparison.OrdinalIgnoreCase))
            {
                // Переключаем паузу в WebView2
                await _mainWindow.HiddenBrowser.ExecuteScriptAsync(_browserMusicPlayer._isPlaying ? "document.querySelector('video').pause();" : "document.querySelector('video').play();");

                // Инвертируем иконку на самой строчке плеера (▶ / ⏸)
                trackData.IsPlaying = !_browserMusicPlayer._isPlaying;
                return;
            }

            // 3. ЛОГИКА ВКЛЮЧЕНИЯ НОВОГО ТРЕКА
            try
            {
                // ПУНКТ 3: Предыдущий трек возвращаем к изначальному состоянию
                if (_lastPlayedTrack != null)
                {
                    _lastPlayedTrack.IsPlaying = false; // Сбрасываем зеленую подсветку
                }
                if (_lastPlayedButton != null)
                {
                    _lastPlayedButton.IsEnabled = true; // Активируем кнопку обратно
                }

                // ПУНКТ 1: Пошла загрузка нового трека
                btn.IsEnabled = false; // Твой XAML-стиль мгновенно прячет '▶', показывает '⌛' и крутит анимацию
                _mainWindow.BottomTrackTitle.Text = "Поиск...";

                // Синхронно запускаем крутилку загрузки на большой нижней кнопке
                //_mainWindow.Dispatcher.Invoke(() =>
                //{
                //    _mainWindow.GlobalPlayPauseBtn.Content = "\uE10C";
                //    _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);
                //    var sb = _mainWindow.FindResource("RotateAnimation") as System.Windows.Media.Animation.Storyboard;
                //    sb?.Begin(_mainWindow, true);
                //});

                // Запрос ссылки на стрим с твоего бэкенда
                string url = $"https://localhost:7296/api/music/stream?artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(track)}";
                string json = await _client.GetStringAsync(url);

                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(json);

                if (data != null && data.Count >= 2)
                {
                    double.TryParse(data[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double duration);
                    var firstTrack = new TrackWithStreamDto
                    {
                        Artist = artist,
                        Title = track,
                        StreamUrl = data[0],
                        //StreamUrl = "https://music.youtube.com/watch?v=0CNPR2qNzxk&list=RDAMVMWdoXZf-FZyA",
                        Duration = duration
                    };

                    // Передаем трек в WebView2
                    await _browserMusicPlayer.PlayTrack(firstTrack);

                    // ПУНКТ 2: Трек загрузился и отправлен на воспроизведение
                    trackData.IsPlaying = true; // Триггер стиля сам перекрасит иконку в зеленую '⏸'
                    btn.IsEnabled = true;       // Возвращаем кнопку в строй

                    // Запоминаем текущий трек и кнопку как "предыдущие" для следующего клика
                    _lastPlayedTrack = trackData;
                    _lastPlayedButton = btn;

                    // Обновляем текст и обложку на нижней панели MainWindow
                    //_mainWindow.BottomTrackTitle.Text = trackData.Title;
                    //_mainWindow.BottomTrackArtist.Text = trackData.Author;

                    if (!string.IsNullOrEmpty(trackData.ImageUrl))
                    {
                        try
                        {
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage(new Uri(trackData.ImageUrl));
                            _mainWindow.BottomTrackImage.Source = bitmap;
                            _mainWindow.BottomTrackImage.Visibility = Visibility.Visible;
                        }
                        catch
                        {
                            _mainWindow.BottomTrackImage.Visibility = Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        _mainWindow.BottomTrackImage.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                // Если произошла сетевая ошибка — возвращаем кнопку к жизни, чтобы интерфейс не завис
                btn.IsEnabled = true;
                trackData.IsPlaying = false;

                _mainWindow.Dispatcher.Invoke(() => {
                    var sb = _mainWindow.FindResource("RotateAnimation") as System.Windows.Media.Animation.Storyboard;
                    sb?.Stop(_mainWindow);
                    _mainWindow.GlobalPlayPauseBtn.Content = "\uE102";
                });

                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }






        //private TrackDto2 _lastPlayedTrack;

        //private bool _isDataLoading = false;
        //public async void TrackRow_Click(object sender, RoutedEventArgs e)
        //{

        //    if (_isDataLoading) return;
        //    var btn = sender as Button;
        //    var track = btn?.DataContext as TrackDto2;

        //    if (btn == null || track == null) return;

        //    if (_lastPlayedTrack == track)
        //    {
        //        if (track.IsPlaying) { GlobalPlayer.Pause(); track.IsPlaying = false; }
        //        else { GlobalPlayer.Resume(); track.IsPlaying = true; }
        //        return;
        //    }

        //    try
        //    {
        //        btn.IsEnabled = false;
        //        _isDataLoading = true;
        //        if (_lastPlayedTrack != null) _lastPlayedTrack.IsPlaying = false;



        //        string artist = track.Author;
        //        string title = track.Title;


        //        string streamUrl = $"https://localhost:7296/api/music/stream?artist={artist}&track={title}";
        //        string test = $"https://localhost:7296/api/music/stream?artist=judas%20priest&track=creatures";
        //        GlobalPlayer.CurrentTrack = track;

        //        System.Diagnostics.Debug.WriteLine("Отправляю в плеер: " + test);
        //        System.Diagnostics.Debug.WriteLine("ДИНАМИЧЕСКИЙ URL: " + streamUrl);

        //        var tcs = new TaskCompletionSource<bool>();
        //        GlobalPlayer.OnPlayingStarted += () => tcs.TrySetResult(true);

        //        // Теперь UriFormatException пропадет
        //        GlobalPlayer.Play(streamUrl);

        //        await Task.WhenAny(tcs.Task, Task.Delay(10000));
        //        track.IsPlaying = true;
        //        _lastPlayedTrack = track;

        //    }
        //    catch (OperationCanceledException)
        //    {
        //        System.Diagnostics.Debug.WriteLine("[DEBUG] Запрос отменен, так как выбран другой трек.");
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"Ошибка стриминга: {ex.Message}");
        //    }
        //    finally
        //    {
        //        btn.IsEnabled = true; 
        //        _isDataLoading = false;
        //    }
        //}

        //// Вспомогательный класс для десериализации
        //public class StreamResponse
        //{
        //    [JsonPropertyName("url")]
        //    public string Url { get; set; }
        //}

    }
}
