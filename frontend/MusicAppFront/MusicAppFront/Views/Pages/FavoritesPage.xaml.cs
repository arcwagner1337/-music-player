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

            _client.BaseAddress = new Uri(App.Settings.BaseAddress);


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
         
                        bool isMatch = string.Equals(trackk.Title?.Trim(), _nativePlayer._currentlyPlayingTrack.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(trackk.Author?.Trim(), _nativePlayer._currentlyPlayingTrack.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (!isMatch)
                        {
                         
                            trackk.IsPlaying = false;
                            break; 
                        }
                    }
                }


                System.Diagnostics.Debug.WriteLine("очереди очищены");
                _nativePlayer._currentlyPlayingTrack = null;


                _nativePlayer._historyStackAlbum.Clear();
                _nativePlayer._forwardStackAlbum.Clear();
    
            }

            var btn = sender as Button;
            var trackData = btn?.DataContext as SearchResultDto.TrackDto2;

            if (trackData == null || btn == null) return; 
            if (_isDataLoading) return; 

            string artist = trackData.Author;
            string track = trackData.Title;

  
            var currentPlaying = _nativePlayer._currentlyPlayingTrack;


            var currentList = FavoriteTracks;


            for (int i = 0; i < currentList.Count; i++)
            {
                if (trackData.Title == currentList[i].Title && trackData.Author == currentList[i].Author)
                {
                    targetIndex = i; break;
                }
            }


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

    
            try
            {
                _isDataLoading = true;

         
                if (_lastPlayedTrack != null) _lastPlayedTrack.IsPlaying = false;
                if (_lastPlayedButton != null) _lastPlayedButton.IsEnabled = true;

                btn.IsEnabled = false;
                _mainWindow.BottomTrackTitle.Text = "Поиск ссылки...";

                string url = $"{App.Settings.BaseAddress}api/music/stream?artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(track)}";


                string json = await _client.GetStringAsync(url);
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(json);

                if (data != null && data.Count >= 2)
                {
                    double.TryParse(data[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double duration);

                    var trackToBePlayed = new TrackWithStreamDto
                    {
                        Artist = artist,
                        Title = track,
                        YtUrl = data[0], 
                        Duration = duration,
                        IsResolved = false,
                        ImageUrl = trackData.ImageUrl 
                    };

                    _mainWindow.BottomTrackTitle.Text = "Резолв аудио...";

      
                    trackToBePlayed.StreamUrl = await _nativePlayer.ResolveAudioUrlAsync(trackToBePlayed.YtUrl);
                    trackToBePlayed.IsResolved = true;

                    if (string.IsNullOrEmpty(trackToBePlayed.StreamUrl))
                    {
                        MessageBox.Show("Не удалось получить аудиопоток. Все сервера yt-dlp недоступны.");
                        btn.IsEnabled = true;
                        return;
                    }

                    await _nativePlayer.PlayAlbumTrack(trackToBePlayed, addToHistory: true, clearForward: true);


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
                YtUrl = null, 
                Duration = 0, 
                IsResolved = false
            };
        }



        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
