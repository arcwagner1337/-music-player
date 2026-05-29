using MusicAppFront.Models;
using MusicAppFront.Views.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using testPlayer;
using static MusicAppFront.Models.SearchResultDto;
using static MusicAppFront.Views.Windows.MainWindow;
using static testPlayer.NativePlayer;
using TrackDto2 = MusicAppFront.Models.SearchResultDto.TrackDto2;

namespace MusicAppFront.Views.Pages
{
    /// <summary>
    /// Логика взаимодействия для FavoritesPage.xaml
    /// </summary>
    public partial class FavoritesPage : Page
    {
        private HttpClient _client = new HttpClient();
        private readonly MainWindow _mainWindow;
        private readonly NativePlayer _nativePlayer;
        private Button _lastPlayedButton;
        public ObservableCollection<TrackDto2> FavoriteTracks { get; set; } = new();
        public FavoritesPage(MainWindow mainwindow, NativePlayer nativePlayer)
        {
            _mainWindow = mainwindow;
            _nativePlayer = nativePlayer;
            _client = new HttpClient();
            _client.BaseAddress = new Uri("https://localhost:7296/"); 
            InitializeComponent();
            this.DataContext = this;
            
            this.Loaded += (s, e) => LoadFavorites();

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
        }
        private bool _isDataLoading = false;
        private int targetIndex = -1;
        private SearchResultDto.TrackDto2 _lastPlayedTrack;

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


        public async void TrackRow_Click(object sender, RoutedEventArgs e)
        {
            if (_nativePlayer != null)
            {
                _mainWindow.isAlbumOpenAndActive = true;
                if (_nativePlayer._currentlyPlayingTrack != null && _mainWindow.GlobalAlbumResults.Tracks != null)
                {
                    foreach (var trackk in _mainWindow.GlobalAlbumResults.Tracks)
                    {
                        // Сверяем название и автора (как в твоем методе клика)
                        bool isMatch = string.Equals(trackk.Title?.Trim(), _nativePlayer._currentlyPlayingTrack.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(trackk.Author?.Trim(), _nativePlayer._currentlyPlayingTrack.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (!isMatch)
                        {
                            // Нашли! Меняем флаг в "сырых" данных до инициализации UI
                            trackk.IsPlaying = false;
                            break; // Выходим из цикла
                        }
                    }
                }


                System.Diagnostics.Debug.WriteLine("очереди очищены");
                _nativePlayer._currentlyPlayingTrack = null;


                _nativePlayer._historyStackAlbum.Clear();
                _nativePlayer._forwardStackAlbum.Clear();///потом сравнение альбомов надо сделать и очищать только если альбом другой
            //_nativePlayer._playbackAlbumQueue.Clear();
            }

            var btn = sender as Button;
            var trackData = btn?.DataContext as SearchResultDto.TrackDto2;

            if (trackData == null || btn == null) return; // Защита от NullReference
            if (_isDataLoading) return; // Защита от спама кликами

            string artist = trackData.Author;
            string track = trackData.Title;

            // 1. ЛОГИКА ПОВТОРНОГО КЛИКА (Ставим на паузу или снимаем с нее)
            var currentPlaying = _nativePlayer._currentlyPlayingTrack;


            var currentList = FavoriteTracks;

            //for (int i = 0; i < _mainWindow.GlobalAlbumResults.Tracks.Count; i++)
            //{
            //    if (trackData.Title == _mainWindow.GlobalAlbumResults.Tracks[i].Title && trackData.Author == _mainWindow.GlobalAlbumResults.Tracks[i].Author)
            //    {
            //        targetIndex = i; break;
            //    }
            //}

            for (int i = 0; i < currentList.Count; i++)
            {
                if (trackData.Title == currentList[i].Title && trackData.Author == currentList[i].Author)
                {
                    targetIndex = i; break;
                }
            }


            //for (int i = 0; i < targetIndex; i++)
            //{
            //    _nativePlayer._historyStackAlbum.Push(FromSearchResult(_mainWindow.GlobalAlbumResults.Tracks[i]));
            //}

            for (int i = 0; i < targetIndex; i++)
            {
                _nativePlayer._historyStackAlbum.Push(FromSearchResult(currentList[i]));
            }


            for (int i = currentList.Count - 1; i > targetIndex; i--)
            {
                _nativePlayer._forwardStackAlbum.Push(FromSearchResult(currentList[i]));
            }


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
                    await _nativePlayer.PlayAlbumTrack(trackToBePlayed, addToHistory: true, clearForward: true);

                    // Меняем иконки на кнопках на состояние "Играет"
                    trackData.IsPlaying = true;
                    btn.IsEnabled = true;

                    _mainWindow.GlobalPlayPauseBtn.Content = "\uE103"; // Пауза
                    _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);

                    _lastPlayedTrack = trackData;
                    _lastPlayedButton = btn;

                    //_nativePlayer._historyStackAlbum.Clear();
                    //_nativePlayer._forwardStackAlbum.Clear();///потом сравнение альбомов надо сделать и очищать только если альбом другой
                    //_nativePlayer._playbackAlbumQueue.Clear();
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












        private async void LoadFavorites()
        {


            try
            {
                _mainWindow.GlobalAlbumResults.Tracks.Clear();

                var token = AuthStorage.AuthStorage.GetToken();
                if (!string.IsNullOrEmpty(token))
                {
                    _client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
                // Используй свой статический HttpClient или тот, что настроил ранее
                var tracks = await _client.GetFromJsonAsync<List<FavoriteTrack>>("api/music/listFavorites"); 

                FavoriteTracks.Clear();
                foreach (var track in tracks)
                {
                    var newTrack = new TrackDto2
                    {
                        Title = track.Title,
                        Author = track.Author,
                        ImageUrl = track.ImageUrl,

                    };




                    newTrack.SetFavoriteSilently(true);
                    _mainWindow.GlobalAlbumResults.Tracks.Add(newTrack);
                    FavoriteTracks.Add(newTrack);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки избранного: {ex.Message}");
            }
        }

        public static TrackWithStreamDto FromSearchResult(SearchResultDto.TrackDto2 searchTrack)
        {
            return new TrackWithStreamDto
            {
                Artist = searchTrack.Author,
                Title = searchTrack.Title,
                ImageUrl = searchTrack.ImageUrl,
                YtUrl = null, // Это поле заполнится позже, когда дернешь /api/music/stream
                Duration = 0, // Или распарси, если есть в DTO
                IsResolved = false
            };
        }



        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
