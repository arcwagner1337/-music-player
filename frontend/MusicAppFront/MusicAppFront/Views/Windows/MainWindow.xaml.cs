using LibVLCSharp.Shared;
using MusicAppFront.browserMusicPlayer;
using MusicAppFront.Models;
using MusicAppFront.Resources;
using MusicAppFront.Views.Pages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using static MusicAppFront.browserMusicPlayer.BrowserMusicPlayer;
using static MusicAppFront.Models.SearchResultDto;
using static testPlayer.NativePlayer;

namespace MusicAppFront.Views.Windows
{
    public partial class MainWindow : Window
    {
        //public static string _currentUserName = MusicAppFront.Views.Windows.MainWindow._currentUserName;
        public static string _currentUserName = "";

        private HomePage _homePage;
        private ProfilePage _profilePage;
        private FavoritesPage _favoritesPage;
        private PlaylistsPage _playlistsPage;
        private MaxFlowPage _maxFlowPage;
        //private BrowserMusicPlayer _browserMusicPlayer;

        private testPlayer.NativePlayer _nativePlayer;

        private HttpClient _client = new HttpClient();

        public SearchResultDto GlobalResults = new SearchResultDto();
        public SearchResultDto GlobalAlbumResults = new SearchResultDto();
        public ObservableCollection<testPlayer.NativePlayer.TrackWithStreamDto> HistoryList = new ObservableCollection<testPlayer.NativePlayer.TrackWithStreamDto>();
        public ObservableCollection<string> UserPlaylists { get; set; } = new ObservableCollection<string>();
        private MusicAppFront.Views.Pages.FullPlayerPage _singleFullPlayerPage;

        public bool isAlbumOpenAndActive = false;

        public class FavoriteTrack
        {
            public int Id { get; set; }
            public string Username { get; set; } = string.Empty;

            public string Title { get; set; } = string.Empty;

            public string Author { get; set; } = string.Empty;
            public string ImageUrl { get; set; } = string.Empty;

        }
        public static MainWindow Instance { get; private set; }

        public void InitPlaylistCommandBindings()
        {
            // Регистрируем обработчик для добавления трека
            CommandBindings.Add(new CommandBinding(PlaylistCommands.AddTrackToPlaylist, ExecuteAddTrackToPlaylist));
            // Регистрируем обработчик для редиректа
            CommandBindings.Add(new CommandBinding(PlaylistCommands.RedirectToCreatePlaylist, ExecuteRedirectToCreatePlaylist));

            CommandBindings.Add(new CommandBinding(PlaylistCommands.OpenPlaylist, ExecuteOpenPlaylist));

            // Сразу же подгрузим плейлисты один раз при старте
            _ = RefreshUserPlaylistsAsync();
        }

        // 1. Метод подгрузки списка плейлистов (вызывай его также при успешном создании нового плейлиста!)
        public async Task RefreshUserPlaylistsAsync()
        {
            try
            {
                // Ждем, пока подгрузится реальный _currentUserName из токена
                // Подставил "Alexander", "asd" или пустую строку — на случай дефолтов
                while (string.IsNullOrEmpty(_currentUserName) || _currentUserName == "asd" || _currentUserName == "Alexander")
                {
                    await Task.Delay(100);
                }

                // Анонимный объект, в точности повторяющий твою JSON-структуру для тела запроса
                var requestBody = new { username = _currentUserName };

                // Отправляем POST запрос вместо GET
                var response = await _client.PostAsJsonAsync("api/music/user-all-playLists", requestBody);

                if (response.IsSuccessStatusCode)
                {
                    // Читаем JSON документ, чтобы вытащить названия без создания новых классов-моделей
                    using (var jsonDoc = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>())
                    {
                        if (jsonDoc != null)
                        {
                            UserPlaylists.Clear();

                            // Если бэк возвращает массив объектов (например: [ { "id": 1, "playlistName": "Для работы" }, ... ])
                            if (jsonDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                foreach (var item in jsonDoc.RootElement.EnumerateArray())
                                {
                                    // Проверяем свойство с именем плейлиста. 
                                    // Посмотри в свагере, как оно точно называется: "playlistName", "name" или "title"
                                    if (item.TryGetProperty("playlistName", out var nameProp) ||
                                        item.TryGetProperty("name", out nameProp))
                                    {
                                        string pName = nameProp.GetString();
                                        if (!string.IsNullOrEmpty(pName))
                                        {
                                            UserPlaylists.Add(pName);
                                        }
                                    }
                                }
                            }
                            // Если вдруг бэк возвращает просто массив строк (например: [ "Для работы", "Для уборки" ])
                            else if (jsonDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                foreach (var item in jsonDoc.RootElement.EnumerateArray())
                                {
                                    UserPlaylists.Add(item.GetString());
                                }
                            }
                        }
                    }
                    Debug.WriteLine($"[MainWindow] Успешно синхронизировано плейлистов: {UserPlaylists.Count}");
                }
                else
                {
                    Debug.WriteLine($"[MainWindow] Бэк ответил ошибкой при получении плейлистов: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка обновления плейлистов: {ex.Message}");
            }
        }

        // 2. Логика клика по кнопке плейлиста в Popup
        private async void ExecuteAddTrackToPlaylist(object sender, ExecutedRoutedEventArgs e)
        {
            //string targetPlaylistName = e.Parameter as string; // Получили имя плейлиста из CommandParameter

            //// Ищем, какой трек сейчас выбран/находится под мышкой в текущем контексте данных.
            //// e.OriginalSource — это кнопка, на которую нажали. Её DataContext в родителе — это наш трек TrackDto2
            //var element = e.OriginalSource as FrameworkElement;
            //var track = element?.DataContext as SearchResultDto.TrackDto2;

            e.Handled = true;

            if (e.Parameter is object[] values && values.Length >= 2)
            {
                // 1. Достаем имя плейлиста
                string targetPlaylistName = values[0] as string;

                // 2. Достаем трек напрямую, без обхода деревьев!
                // Замени YourTrackClass на свой реальный класс трека (например, TrackDto2)
                var track = values[1] as SearchResultDto.TrackDto2;

                // Проверяем, что поймали
                MessageBox.Show($"Клик сработал!\nПлейлист: {targetPlaylistName}\nТрек: {track?.Title ?? "НЕ НАЙДЕН"}");

                if (track != null && !string.IsNullOrEmpty(targetPlaylistName))
                {
                    var payload = new
                    {
                        PlaylistName = targetPlaylistName,
                        Username = _currentUserName,
                        TrackTitle = track.Title,
                        TrackArtist = track.Author,
                        ImageUrl = track.ImageUrl
                    };

                    try
                    {
                        var response = await _client.PostAsJsonAsync("api/music/add-track-to-playlist", payload);
                        if (response.IsSuccessStatusCode)
                        {
                            MessageBox.Show($"Трек добавлен в плейлист \"{targetPlaylistName}\"!");
                        }
                        else
                        {
                            MessageBox.Show("Не удалось добавить трек.");
                        }
                    }
                    catch (Exception ex) { MessageBox.Show($"Ошибка сети: {ex.Message}"); }
                }
            }
            else
            {
                MessageBox.Show("Ошибка: Параметры команды не дошли.");
            }
        }

        // 3. Логика клика по кнопке "+ Создать плейлист" в Popup
        private void ExecuteRedirectToCreatePlaylist(object sender, ExecutedRoutedEventArgs e)
        {
            e.Handled = true;
            // Перенаправляем главную страницу на создание плейлиста
            if (MainFrame != null)
            {
                MainFrame.Navigate(new MusicAppFront.Views.Pages.CreatePlaylist(this));
            }
        }

        private void ExecuteOpenPlaylist(object sender, ExecutedRoutedEventArgs e)
        {
            e.Handled = true;

            // В параметр (e.Parameter) прилетает строка с названием плейлиста
            string playlistName = e.Parameter as string;

            if (!string.IsNullOrEmpty(playlistName))
            {
                var fakeAlbum = new SearchResultDto.AlbumDto(
                    playlistName,                                         // Name
                    "pack://application:,,,/Resources/default_playlist.png", // ImageUrl
                    playlistName,                              // Id (с меткой для бэка/фронта)
                    null,                                                 // Url
                    null                                                  // Playcount
                );

                MessageBox.Show($"Открываем плейлист: {playlistName}"); // Временная заглушка для проверки
            }
        }


        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            InitPlaylistCommandBindings();
            _client = new HttpClient();
            _client.BaseAddress = new Uri("https://localhost:7296/");

            _ = LoadCurrentUserDataAsync();

            _profilePage = new ProfilePage();
            _playlistsPage = new PlaylistsPage();
            _maxFlowPage = new MaxFlowPage();

            _nativePlayer = new testPlayer.NativePlayer(this);
            _favoritesPage = new FavoritesPage(this, _nativePlayer);
            _homePage = new HomePage(this, _nativePlayer);
            MainFrame.Navigate(_homePage);

            //_browserMusicPlayer = new BrowserMusicPlayer(this);
            //_browserMusicPlayer.InitBrowser();

            LibVLCSharp.Shared.Core.Initialize();
            _nativePlayer._libVlc = new LibVLC();
            _nativePlayer._mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_nativePlayer._libVlc);

            var loadedList = _nativePlayer.GetHistory(); // Твой метод, который читает JSON

            // Заполняем коллекцию
            foreach (var track in loadedList)
            {
                HistoryList.Add(track);
            }
            //InitializeComponent();

            _ = Task.Run(async () =>
            {
                Console.WriteLine("прогрев сервулятора");
                try
                {
                    await _client.GetAsync("http://localhost:8888/");
                    Console.WriteLine("сервулятор прогрет)");
                }
                catch { }
            });

            _nativePlayer._mediaPlayer.EndReached += (s, e) =>
            {

                Task.Run(async () =>
                {
                    try
                    {
                        // Небольшая задержка, чтобы VLC гарантированно перешел в состояние Stopped
                        await Task.Delay(50);

                        // Запускаем переключение трека в фоне

                        if (isAlbumOpenAndActive) { await _nativePlayer.PlayNextAlbumTrackAsync(GlobalAlbumResults); }
                        else { await _nativePlayer.PlayNextTrackAsync(GlobalResults); }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при переключении: {ex.Message}");
                    }
                });
            };


            _nativePlayer._mediaPlayer.TimeChanged += (s, e) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    double currentTime = e.Time / 1000.0; // VLC отдаёт миллисекунды

                    if (!_nativePlayer._isDragging && currentTime >= 0 && currentTime <= TimelineSlider.Maximum)
                    {
                        TimelineSlider.Value = currentTime;
                        TotalTimeText.Text = $"{_nativePlayer.FormatTime(TimelineSlider.Maximum)}";
                        CurrentTimeText.Text = $"{_nativePlayer.FormatTime(TimelineSlider.Value)}";
                    }
                });
            };


        }

        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {

            await _nativePlayer.test();

        }


        private async void GlobalPlayPauseBtn_Click(object sender, RoutedEventArgs e)
        {

            if (isAlbumOpenAndActive)
            {
                _nativePlayer.BtnPlay_Click(sender, e, GlobalAlbumResults);
            }
            else
            {
                _nativePlayer.BtnPlay_Click(sender, e, GlobalResults);
            }
        }



        private void NextBtn_Click(object sender, RoutedEventArgs e)
        {

            if (isAlbumOpenAndActive)
            {

                _nativePlayer.BtnNextAlbum_Click(sender, e, GlobalAlbumResults);
            }
            else
            {
                _nativePlayer.BtnNext_Click(sender, e, GlobalResults);
            }
        }

        private void PrevBtn_Click(object sender, RoutedEventArgs e)
        {

            if (isAlbumOpenAndActive)
            {
                _nativePlayer.BtnPrevAlbum_Click(sender, e, GlobalAlbumResults);
            }
            else
            {
                _nativePlayer.BtnPrev_Click(sender, e, GlobalResults);
            }
        }

        private void TimelineSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _nativePlayer.TimelineSlider_PreviewMouseLeftButtonDown(sender, e);
        }

        private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _nativePlayer.TimelineSlider_ValueChanged(sender, e);

        }


        private void TimelineSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _nativePlayer.TimelineSlider_DragStarted(sender, e);
        }

        private async void TimelineSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _nativePlayer.TimelineSlider_DragCompleted(sender, e);
        }















        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {

        }





        private void HomeTab_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content != _homePage)
                MainFrame.Navigate(_homePage);
        }
        private void ProfileTab_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content != _profilePage)
                MainFrame.Navigate(_profilePage);
        }
        private void FavoritesTab_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content != _favoritesPage)
                MainFrame.Navigate(_favoritesPage);
        }
        private void PlaylistsTab_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content != _playlistsPage)
                MainFrame.Navigate(_playlistsPage);
        }
        private void MaxFlowTab_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content != _maxFlowPage)
                MainFrame.Navigate(_maxFlowPage);
        }

        private void MainFrame_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as FrameworkElement;
            while (element != null)
            {
                // Проверяем, что это наша карточка
                if (element is ContentControl cc)
                {
                    if (cc.Style == (Style)FindResource("PlaylistCardStyle"))
                    {

                        // Достаем данные альбома из DataContext этой карточки
                        if (cc.DataContext is AlbumDto album)
                        {
                            // Передаем альбом в конструктор страницы
                            //isAlbumOpen = true;
                            MainFrame.Navigate(new InfoPlaylistPage(album, this, _nativePlayer));
                        }
                        else if (cc.DataContext is string playlistName)
                        {
                            // Мапим строку под формат AlbumDto через его конструктор
                            var fakeAlbum = new SearchResultDto.AlbumDto(
                                playlistName,                                             // Name
                                "pack://application:,,,/Resources/default_playlist.png",    // ImageUrl (твоя заглушка)
                                "local_" + playlistName,                                  // Id с префиксом для бэкенда
                                null,                                                     // Url
                                null                                                      // Playcount
                            );

                            // Передаем созданный фейковый альбом в ту же самую страницу!
                            MainFrame.Navigate(new InfoPlaylistPage(fakeAlbum, this, _nativePlayer));
                        }
                        else
                        {
                            // Если данных нет, просто открываем (как было), 
                            // но лучше проверить, почему DataContext пустой
                            //isAlbumOpen = true;
                            MainFrame.Navigate(new InfoPlaylistPage(null, this, _nativePlayer));
                        }

                        e.Handled = true;
                        break;

                    }

                    else if (cc.Style == (Style)FindResource("CreateCardStyle"))
                    {
                        MainFrame.Navigate(new CreatePlaylist(this));
                        e.Handled = true;
                        break;
                    }
                }
                element = VisualTreeHelper.GetParent(element) as FrameworkElement;
            }
        }

        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                try
                {
                    // Делаем запрос. Используем GetFromJsonAsync, он сам десериализует ответ
                    // Если бэк требует токен, добавим заголовок (как обсуждали раньше)
                    var results = await _client.GetFromJsonAsync<SearchResultDto>(
                        $"api/music/search?query={Uri.EscapeDataString(SearchBox.Text)}"
                    );

                    var token = AuthStorage.AuthStorage.GetToken();
                    if (!string.IsNullOrEmpty(token))
                    {
                        _client.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    }

                    var favorites = await _client.GetFromJsonAsync<List<FavoriteTrack>>("api/music/listFavorites");

                    if (results?.Tracks != null && favorites != null)
                    {
                        foreach (var track in results.Tracks)
                        {
                            bool isFav = favorites?.Any(f =>
                                    string.Equals(f.Title?.Trim(), track.Title?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(f.Author?.Trim(), track.Author?.Trim(), StringComparison.OrdinalIgnoreCase)
                                ) ?? false;

                            track.SetFavoriteSilently(isFav);

                            System.Diagnostics.Debug.WriteLine($"isFav {track.Title}: {track.IsFavorite}");
                        }
                    }

                    if (results != null)
                    {
                        GlobalResults = results;
                        foreach (var res in GlobalResults.Tracks)
                        {
                            System.Diagnostics.Debug.WriteLine(":GlobalResults " + res.Title);
                        }
                        // 1. Проверяем, играет ли что-то в плеере прямо сейчас
                        var currentPlaying = _nativePlayer?._currentlyPlayingTrack;
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
                                    track.IsPlaying = _nativePlayer._mediaPlayer.IsPlaying;
                                    break; // Выходим из цикла
                                }
                            }
                        }

                        // 2. Передаем уже ИЗМЕНЕННЫЕ результаты в конструктор страницы
                        var searchPage = new SearchPage(results, this, _nativePlayer);

                        // 3. Передаем ссылку на измененный трек внутрь страницы, 
                        // чтобы кнопка "паузы" знала, кого сбрасывать при следующем клике
                        if (results.Tracks != null)
                        {
                            searchPage._lastPlayedTrack = results.Tracks.FirstOrDefault(t => t.IsPlaying);
                        }

                        // 4. Отправляем готовую страницу во Frame
                        MainFrame.Navigate(searchPage);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка поиска: {ex.Message}");
                }
            }
        }


        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        private void OpenFullPlayer_Click(object sender, MouseButtonEventArgs e)
        {
            if (MainFrame.Content is MusicAppFront.Views.Pages.FullPlayerPage)
            {



                // Уходим назад (на страницу поиска или хоум)
                if (MainFrame.CanGoBack)
                {
                    MainFrame.GoBack();
                }
                return;
            }
            if (_singleFullPlayerPage == null)
            {
                _singleFullPlayerPage = new FullPlayerPage(this, _nativePlayer, GlobalResults, GlobalAlbumResults);
            }


            MainFrame.Navigate(new FullPlayerPage(this, _nativePlayer, GlobalResults, GlobalAlbumResults));
        }


        private async Task LoadCurrentUserDataAsync()
        {
            try
            {
                string token = AuthStorage.AuthStorage.GetToken();
                if (string.IsNullOrEmpty(token)) return;

                var request = new HttpRequestMessage(HttpMethod.Get, "api/user/me");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    // Читаем JSON "на лету" как документ без привязки к классам
                    using (var jsonDoc = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>())
                    {
                        if (jsonDoc != null && jsonDoc.RootElement.TryGetProperty("username", out var usernameRoot))
                        {
                            string username = usernameRoot.GetString();
                            if (!string.IsNullOrWhiteSpace(username))
                            {
                                _currentUserName = username;
                                Debug.WriteLine($"[MainWindow] Юзер успешно подгружен без DTO: {_currentUserName}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Ошибка загрузки юзера: {ex.Message}");
            }
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
