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
using testPlayer;
using static MusicAppFront.Models.SearchResultDto;
using static testPlayer.NativePlayer;


namespace MusicAppFront.Views.Pages
{
    /// <summary>
    /// Логика взаимодействия для SearchPage.xaml
    /// </summary>
    public partial class SearchPage : Page
    {
        //private BrowserMusicPlayer _browserMusicPlayer;
        private readonly MainWindow _mainWindow;
        private HttpClient _client = new HttpClient();
        private Button _lastPlayedButton;
        internal SearchResultDto.TrackDto2 _lastPlayedTrack;
        private testPlayer.NativePlayer _nativePlayer;

        // Защита от спама кликами, пока идет тяжелый запрос к API
        private bool _isDataLoading = false;
        public SearchPage(SearchResultDto results, MainWindow mainWindow, NativePlayer player)
        {
            InitializeComponent();
            ArtistsList.ItemsSource = results.Artists;
            TracksList.ItemsSource = results.Tracks;
            AlbumsList.ItemsSource = results.Albums;



            // 4. Ищем конкретный трек прямо в списке данных results.Tracks!
            this.LayoutUpdated += (s, e) =>
            {
                if (_lastPlayedTrack != null && _lastPlayedButton == null)
                {
                    var listBoxItem = TracksList.ItemContainerGenerator.ContainerFromItem(_lastPlayedTrack) as ListBoxItem;
                    if (listBoxItem != null)
                    {
                        _lastPlayedButton = GetButtonFromContainer(listBoxItem);
                    }
                }
            };

            _mainWindow = mainWindow;
            _nativePlayer = player;
            //RefreshTrackIcons();
            //GlobalPlayer.Init();
        }

        private Button GetButtonFromContainer(DependencyObject parent)
        {
            if (parent is Button button) return button;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var result = GetButtonFromContainer(child);
                if (result != null) return result;
            }
            return null;
        }


        //public void RefreshTrackIcons()
        //{
        //    var currentPlaying = _nativePlayer?._currentlyPlayingTrack;
        //    bool isMediaPlayerPlaying = _nativePlayer?._mediaPlayer?.IsPlaying ?? false;

        //    // 1. Сохраняем ссылку на текущий список треков
        //    var tracks = TracksList.ItemsSource as System.Collections.IEnumerable;
        //    if (tracks == null) return;

        //    // 2. Обновляем флаги IsPlaying в данных
        //    foreach (var item in tracks)
        //    {
        //        var trackData = item as SearchResultDto.TrackDto2;
        //        if (trackData == null) continue;

        //        if (currentPlaying != null &&
        //            trackData.Title.Equals(currentPlaying.Title, StringComparison.OrdinalIgnoreCase) &&
        //            trackData.Author.Equals(currentPlaying.Artist, StringComparison.OrdinalIgnoreCase))
        //        {
        //            trackData.IsPlaying = isMediaPlayerPlaying;
        //            _lastPlayedTrack = trackData;
        //        }
        //        else
        //        {
        //            trackData.IsPlaying = false;
        //        }
        //    }

        //    // 3. Жесткий пинок для ItemsControl: сбрасываем и накатываем список заново.
        //    // Это заставит триггеры в XAML сработать и перерисовать Play/Pause мгновенно!
        //    TracksList.ItemsSource = null;
        //    TracksList.ItemsSource = tracks;
        //}

        public async void TrackRow_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is CheckBox || e.OriginalSource is TextBlock && (e.OriginalSource as TextBlock).Parent is CheckBox)
            {
                return;
            }


            var btn = sender as Button;
            var trackData = btn?.DataContext as SearchResultDto.TrackDto2;
            _mainWindow.isAlbumOpenAndActive = false;

            if (trackData == null || btn == null) return; // Защита от NullReference
            if (_isDataLoading) return; // Защита от спама кликами

            string artist = trackData.Author;
            string track = trackData.Title;

            // 1. ЛОГИКА ПОВТОРНОГО КЛИКА (Ставим на паузу или снимаем с нее)
            var currentPlaying = _nativePlayer._currentlyPlayingTrack;

            if (currentPlaying != null &&
                currentPlaying.Title.Equals(track, StringComparison.OrdinalIgnoreCase) &&
                currentPlaying.Artist.Equals(artist, StringComparison.OrdinalIgnoreCase))
            {
                if (_nativePlayer._mediaPlayer.IsPlaying)
                {
                    _nativePlayer._mediaPlayer.Pause();
                    trackData.IsPlaying = false;

                    // Синхронизируем глобальную кнопку внизу окна
                    _mainWindow.GlobalPlayPauseBtn.Content = "\uE102"; // Иконка Play
                    _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(2, 0, 0, 0);
                }
                else
                {
                    _nativePlayer._mediaPlayer.Play();
                    trackData.IsPlaying = true;

                    _mainWindow.GlobalPlayPauseBtn.Content = "\uE103"; // Иконка Pause
                    _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);
                }
                
                return;
            }

            // 2. ЛОГИКА ВКЛЮЧЕНИЯ НОВОГО ТРЕКА
            try
            {
                _isDataLoading = true;

                // Сбрасываем визуальное состояние старого трека
                if (_lastPlayedTrack != null) _lastPlayedTrack.IsPlaying = false;
                if (_lastPlayedButton != null) _lastPlayedButton.IsEnabled = true;

                btn.IsEnabled = false;
                _mainWindow.BottomTrackTitle.Text = "Поиск ссылки...";

                // Шаг А: Спрашиваем у локального бэкенда YouTube URL
                string url = $"https://localhost:7296/api/music/stream?artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(track)}";
                string json = await _client.GetStringAsync(url);
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(json);

                if (data != null && data.Count >= 2)
                {
                    double.TryParse(data[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double duration);

                    var trackToBePlayed = new TrackWithStreamDto
                    {
                        Artist = artist,
                        Title = track,
                        YtUrl = data[0], // Сюда падает YouTube URL из бэкенда
                        Duration = duration,
                        IsResolved = false,
                        ImageUrl = trackData.ImageUrl // Сохраняем обложку
                    };

                    _mainWindow.BottomTrackTitle.Text = "Резолв аудио...";

                    // Шаг Б: Тяжелый резолв через ваши питоновские yt-dlp прокси-серверы
                    // Вызываем метод резолва из вашего класса плеера
                    trackToBePlayed.StreamUrl = await _nativePlayer.ResolveAudioUrlAsync(trackToBePlayed.YtUrl);
                    trackToBePlayed.IsResolved = true;

                    if (string.IsNullOrEmpty(trackToBePlayed.StreamUrl))
                    {
                        MessageBox.Show("Не удалось получить аудиопоток. Все сервера yt-dlp недоступны.");
                        btn.IsEnabled = true;
                        return;
                    }

                    // Шаг В: Скармливаем готовую прямую ссылку в VLC
                    await _nativePlayer.PlayTrack(trackToBePlayed, addToHistory: true, clearForward: true);

                    // Меняем иконки на кнопках на состояние "Играет"
                    trackData.IsPlaying = true;
                    btn.IsEnabled = true;

                    _mainWindow.GlobalPlayPauseBtn.Content = "\uE103"; // Пауза
                    _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);
                    
                    _lastPlayedTrack = trackData;
                    _lastPlayedButton = btn;
                }
            }
            catch (Exception ex)
            {
                btn.IsEnabled = true;
                trackData.IsPlaying = false;
                _mainWindow.BottomTrackTitle.Text = "Ошибка";
                MessageBox.Show("Ошибка: " + ex.Message);
            }
            finally
            {
                _isDataLoading = false;
            }
        }

        //private void OnToggleFavorite(object sender, RoutedEventArgs e)
        //{
        //    // Не забываем, чтобы клик не ушел в кнопку воспроизведения!
        //    e.Handled = true;

        //    var checkBox = sender as CheckBox;
        //    var track = checkBox?.DataContext as SearchResultDto.TrackDto2;

        //    if (track != null)
        //    {
        //        checkBox.IsChecked = !checkBox.IsChecked;
        //        // Твоя логика API

        //        System.Diagnostics.Debug.WriteLine($"Toggle favorite for: {track.Title}");
        //    }
        //}






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
