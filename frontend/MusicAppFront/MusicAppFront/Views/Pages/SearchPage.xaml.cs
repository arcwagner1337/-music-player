
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

    public partial class SearchPage : Page
    {
      
        private readonly MainWindow _mainWindow;
        private HttpClient _client = new HttpClient();
        private Button _lastPlayedButton;
        internal SearchResultDto.TrackDto2 _lastPlayedTrack;
        private testPlayer.NativePlayer _nativePlayer;

   
        private bool _isDataLoading = false;
        public SearchPage(SearchResultDto results, MainWindow mainWindow, NativePlayer player)
        {
            InitializeComponent();
            ArtistsList.ItemsSource = results.Artists;
            TracksList.ItemsSource = results.Tracks;
            AlbumsList.ItemsSource = results.Albums;



     
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


     

        public async void TrackRow_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is CheckBox || e.OriginalSource is TextBlock && (e.OriginalSource as TextBlock).Parent is CheckBox)
            {
                return;
            }


            var btn = sender as Button;
            var trackData = btn?.DataContext as SearchResultDto.TrackDto2;
            _mainWindow.isAlbumOpenAndActive = false;

            if (trackData == null || btn == null) return; 
            if (_isDataLoading) return; 

            string artist = trackData.Author;
            string track = trackData.Title;

     
            var currentPlaying = _nativePlayer._currentlyPlayingTrack;

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

        
                    await _nativePlayer.PlayTrack(trackToBePlayed, addToHistory: true, clearForward: true);

  
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

    }
}
