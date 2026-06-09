using LibVLCSharp.Shared;
using MusicAppFront.Models;
using MusicAppFront.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Numerics;
using System.Text;
using System.Text.Json.Serialization;
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
using static testPlayer.NativePlayer;

namespace MusicAppFront.Views.Pages
{

    public partial class InfoPlaylistPage : Page
    {

        private readonly MainWindow _mainWindow;
        private testPlayer.NativePlayer _nativePlayer;
        public static bool isPlaylist = false;

        private string? _customPlaylistName;
        private bool _isCustomPlaylist = false;

        private static readonly CookieContainer _cookieContainer = new CookieContainer();
        private Button? _lastPlayedButton;
        private static readonly HttpClient _client = new HttpClient(new HttpClientHandler


        {
            CookieContainer = _cookieContainer,
            UseCookies = true
        })
        {

            BaseAddress = new Uri(App.Settings?.BaseAddress ?? ""),



            Timeout = TimeSpan.FromSeconds(10)
        };



        public InfoPlaylistPage(AlbumDto? album, MainWindow mainWindow, NativePlayer player)
        {
            InitializeComponent();

            this.DataContext = album;
            //LoadTracks(album.Id);


            this.Loaded += async (s, e) =>
            {
                if (album != null)
                {
                    await LoadTracks(album.Id);
                }
            };

            this.LayoutUpdated += (s, e) =>
            {
                if (_lastPlayedTrack != null && _lastPlayedButton == null)
                {
                    var listBoxItem = TracksList.ItemContainerGenerator.ContainerFromItem(_lastPlayedTrack) as ListBoxItem;
                    if (listBoxItem != null)
                    {
                        var button = GetButtonFromContainer(listBoxItem);
                        if (button  != null)
                        {
                            _lastPlayedButton = button;
                        }
                    }
                }
            };

            _mainWindow = mainWindow;
            _nativePlayer = player;

        }

        private Button? GetButtonFromContainer(DependencyObject parent)
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

        private SearchResultDto.TrackDto2? _lastPlayedTrack;

        private bool _isDataLoading = false;
        private int targetIndex = -1;


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
                _nativePlayer._currentlyPlayingTrack = null!;


                _nativePlayer._historyStackAlbum.Clear();
                _nativePlayer._forwardStackAlbum.Clear();

            }

            var btn = sender as Button;
            var trackData = btn?.DataContext as SearchResultDto.TrackDto2;

            if (trackData == null || btn == null) return;
            if (_isDataLoading) return;

            string artist = trackData.Author;
            string track = trackData.Title;


            var currentPlaying = _nativePlayer?._currentlyPlayingTrack;
            for (int i = 0; i < _mainWindow?.GlobalAlbumResults?.Tracks?.Count; i++)
            {
                if (trackData.Title == _mainWindow.GlobalAlbumResults.Tracks[i].Title && trackData.Author == _mainWindow.GlobalAlbumResults.Tracks[i].Author)
                {
                    targetIndex = i; break;
                }
            }


            for (int i = 0; i < targetIndex; i++)
            {
                //_nativePlayer?._historyStackAlbum.Push(FromSearchResult(_mainWindow.GlobalAlbumResults.Tracks[i]));
                var trackk = _mainWindow?.GlobalAlbumResults?.Tracks?[i];

                
                if (trackk != null)
                {
                    _nativePlayer?._historyStackAlbum.Push(FromSearchResult(trackk));
                }
            }


            //for (int i = _mainWindow.GlobalAlbumResults.Tracks.Count - 1; i > targetIndex; i--)
            //{
            //    _nativePlayer._forwardStackAlbum.Push(FromSearchResult(_mainWindow.GlobalAlbumResults.Tracks[i]));
            //}

            int tracksCount = _mainWindow?.GlobalAlbumResults?.Tracks?.Count ?? 0;

            // 2. Запускаем цикл, только если треки есть
            for (int i = tracksCount - 1; i > targetIndex; i--)
            {
                var trackk = _mainWindow?.GlobalAlbumResults?.Tracks?[i];

                // 3. Проверяем, что трек успешно извлечен, и пушим его
                if (trackk != null)
                {
                    _nativePlayer?._forwardStackAlbum.Push(FromSearchResult(trackk));
                }
            }


            if (currentPlaying != null &&
                currentPlaying.Title.Equals(track, StringComparison.OrdinalIgnoreCase) &&
                currentPlaying.Artist.Equals(artist, StringComparison.OrdinalIgnoreCase))
            {
                //if (_nativePlayer._mediaPlayer.IsPlaying)
                //{
                //    _nativePlayer._mediaPlayer.Pause();
                //    trackData.IsPlaying = false;


                //    _mainWindow.GlobalPlayPauseBtn.Content = "\uE102"; // Иконка Play
                //    _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(2, 0, 0, 0);
                //}
                //else
                //{
                //    _nativePlayer._mediaPlayer.Play();
                //    trackData.IsPlaying = true;

                //    _mainWindow.GlobalPlayPauseBtn.Content = "\uE103"; // Иконка Pause
                //    _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);
                //}

                if (_nativePlayer?._mediaPlayer?.IsPlaying == true)
                {
                    _nativePlayer._mediaPlayer.Pause();
                    trackData.IsPlaying = false;

                    if (_mainWindow?.GlobalPlayPauseBtn != null)
                    {
                        _mainWindow.GlobalPlayPauseBtn.Content = "\uE102"; // Иконка Play
                        _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(2, 0, 0, 0);
                    }
                }
                else
                {
                    // Безопасно запускаем воспроизведение
                    _nativePlayer?._mediaPlayer?.Play();
                    trackData.IsPlaying = true;

                    if (_mainWindow?.GlobalPlayPauseBtn != null)
                    {
                        _mainWindow.GlobalPlayPauseBtn.Content = "\uE103"; // Иконка Pause
                        _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);
                    }
                }


                return;
            }


            try
            {
                _isDataLoading = true;


                if (_lastPlayedTrack != null) _lastPlayedTrack.IsPlaying = false;
                if (_lastPlayedButton != null) _lastPlayedButton.IsEnabled = true;

                btn.IsEnabled = false;
                if (_mainWindow != null)
                {
                    _mainWindow.BottomTrackTitle.Text = "Поиск ссылки...";
                }


                string url = $"{App.Settings?.BaseAddress}api/music/stream?artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(track)}";



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
                    if (_mainWindow != null)
                    {
                        _mainWindow.BottomTrackTitle.Text = "Резолв аудио...";
                    }
                    if (_nativePlayer != null)
                    {
                        trackToBePlayed.StreamUrl = await _nativePlayer.ResolveAudioUrlAsync(trackToBePlayed.YtUrl);
                    }
                    trackToBePlayed.IsResolved = true;

                    if (string.IsNullOrEmpty(trackToBePlayed.StreamUrl))
                    {
                        MessageBox.Show("Не удалось получить аудиопоток. Все сервера yt-dlp недоступны.");
                        btn.IsEnabled = true;
                        return;
                    }
                    if (_nativePlayer != null)
                    {
                        await _nativePlayer.PlayAlbumTrack(trackToBePlayed, addToHistory: true, clearForward: true);
                    }


                    trackData.IsPlaying = true;
                    btn.IsEnabled = true;
                    if (_mainWindow != null)
                    {
                        _mainWindow.GlobalPlayPauseBtn.Content = "\uE103";
                        _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);
                    }

                    _lastPlayedTrack = trackData;
                    _lastPlayedButton = btn;


                }
            }
            catch (Exception ex)
            {
                btn.IsEnabled = true;
                trackData.IsPlaying = false;
                if (_mainWindow != null)
                {
                    _mainWindow.BottomTrackTitle.Text = "Ошибка";
                }
                MessageBox.Show("Ошибка: " + ex.Message);
            }
            finally
            {
                _isDataLoading = false;
            }
        }




        public class PlaylistTrackDto
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }


            [JsonPropertyName("title")]
            public string? TrackTitle { get; set; }


            [JsonPropertyName("artist")]
            public string? TrackArtist { get; set; }

            [JsonPropertyName("imageUrl")]
            public string? ImageUrl { get; set; }
        }
        private async Task LoadTracks(string albumId)
        {
            try
            {



                List<SearchResultDto.TrackDto2> response = null!;


                if (albumId != null && albumId.StartsWith("local_"))
                {
                    string playlistName = albumId.Replace("local_", "");

                    var requestBody = new
                    {
                        username = "asdqwe",
                        playlistName = playlistName
                    };


                    var httpResponse = await _client.PostAsJsonAsync($"{App.Settings?.BaseAddress}api/music/playlist-tracks", requestBody);




                    string rawJson = await httpResponse.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine(rawJson);

                    if (httpResponse.IsSuccessStatusCode)
                    {

                        var rawTracks = await httpResponse.Content.ReadFromJsonAsync<List<PlaylistTrackDto>>();

                        if (rawTracks != null)
                        {

                            response = rawTracks.Select(t => new SearchResultDto.TrackDto2
                            {
                                Title = t.TrackTitle ?? "Без названия",
                                Author = t.TrackArtist ?? "Неизвестный исполнитель",
                                ImageUrl = t.ImageUrl ?? "pack://application:,,,/Resources/default_playlist.png",
                                Url = null!,
                                CleanTitle = t.TrackTitle ?? "Без названия"
                            }).ToList();
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Ошибка бэкенда: {httpResponse.StatusCode}");
                        return;
                    }
                }
                else
                {

                    response = await _client.GetFromJsonAsync<List<SearchResultDto.TrackDto2>>($"{App.Settings?.BaseAddress}api/music/album/{albumId}") ?? new List<SearchResultDto.TrackDto2>();



                }



                if (response != null)
                {

                    TracksList.ItemsSource = response;
                    AlbumAuthor.Text = response[0].Author.ToString();

                    _mainWindow.GlobalAlbumResults.Tracks = response;

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки треков: {ex.Message}");
            }
        }



        public static TrackWithStreamDto FromSearchResult(SearchResultDto.TrackDto2 searchTrack)
        {
            return new TrackWithStreamDto
            {
                Artist = searchTrack.Author,
                Title = searchTrack.Title,
                ImageUrl = searchTrack.ImageUrl,
                YtUrl = null!,
                Duration = 0,
                IsResolved = false
            };
        }


    }
}
