using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
using static MusicAppFront.Models.SearchResultDto;

namespace MusicAppFront.Views.Pages
{
    /// <summary>
    /// Логика взаимодействия для InfoPlaylistPage.xaml
    /// </summary>
    public partial class InfoPlaylistPage : Page
    {
        private static readonly CookieContainer _cookieContainer = new CookieContainer();
        private static readonly HttpClient _client = new HttpClient(new HttpClientHandler


        {
            CookieContainer = _cookieContainer,
            UseCookies = true
        })
        {
            BaseAddress = new Uri("https://localhost:7296/"),
            Timeout = TimeSpan.FromSeconds(10)
        };

        private static SearchPage _sp;

        public InfoPlaylistPage(AlbumDto album)
        {
            InitializeComponent();

            this.DataContext = album;
            LoadTracks(album.Id);

            GlobalPlayer.OnTrackEnded += async () =>
            {
                // Важно: переключение UI должно быть в главном потоке
                await Dispatcher.InvokeAsync(PlayNextTrack);
            };
        }

        private TrackDto2 _lastPlayedTrack;

        private bool _isDataLoading = false;
        public async void TrackRow_Click(object sender, RoutedEventArgs e)
        {

            if (_isDataLoading) return;
            var btn = sender as Button;
            var track = btn?.DataContext as TrackDto2;

            if (btn == null || track == null) return;

            if (_lastPlayedTrack == track)
            {
                if (track.IsPlaying) { GlobalPlayer.Pause(); track.IsPlaying = false; }
                else { GlobalPlayer.Resume(); track.IsPlaying = true; }
                return;
            }

            await StartPlayTrack(track, btn);
        }

        public async Task PlayNextTrack()
        {
            // Достаем список треков из твоего ItemsControl / ListBox
            var tracks = TracksList.ItemsSource as List<TrackDto2>;
            if (tracks == null || tracks.Count == 0) return;

            // Находим индекс трека, который сейчас играет в глобальном плеере
            int currentIndex = tracks.FindIndex(t => t.Title == GlobalPlayer.CurrentTrack?.Title && t.Author == GlobalPlayer.CurrentTrack?.Author);

            if (currentIndex != -1 && currentIndex < tracks.Count - 1)
            {
                var nextTrack = tracks[currentIndex + 1];
                await StartPlayTrack(nextTrack, null);
            }
        }

        public async Task PlayPrevTrack()
        {
            var tracks = TracksList.ItemsSource as List<TrackDto2>;
            if (tracks == null || tracks.Count == 0) return;

            int currentIndex = tracks.FindIndex(t => t.Title == GlobalPlayer.CurrentTrack?.Title && t.Author == GlobalPlayer.CurrentTrack?.Author);

            // Если прошло больше 3 секунд трека, то "Назад" просто перематывает в начало
            // Если начало трека — прыгаем на предыдущий
            if (currentIndex > 0)
            {
                var prevTrack = tracks[currentIndex - 1];
                await StartPlayTrack(prevTrack, null);
            }
            else
            {
                GlobalPlayer.Seek(0);
            }
        }


        private async Task StartPlayTrack(TrackDto2 track, Button btn)
        {
            try
            {
                if (btn != null) btn.IsEnabled = false;
                _isDataLoading = true;

                if (_lastPlayedTrack != null) _lastPlayedTrack.IsPlaying = false;

                string streamUrl = $"https://localhost:7296/api/music/stream?artist={Uri.EscapeDataString(track.Author)}&track={Uri.EscapeDataString(track.Title)}";

                GlobalPlayer.CurrentTrack = track;

                var tcs = new TaskCompletionSource<bool>();
                Action handler = null;
                handler = () => {
                    tcs.TrySetResult(true);
                    GlobalPlayer.OnPlayingStarted -= handler; // Отписываемся, чтобы не копились
                };
                GlobalPlayer.OnPlayingStarted += handler;

                GlobalPlayer.Play(streamUrl);
                await Task.WhenAny(tcs.Task, Task.Delay(10000));

                track.IsPlaying = true;
                _lastPlayedTrack = track;
            }
            finally
            {
                if (btn != null) btn.IsEnabled = true;
                _isDataLoading = false;
            }
        }


        private async Task LoadTracks(string albumId)
        {
            try
            {
                
                var response = await _client.GetFromJsonAsync<List<TrackDto2>>($"https://localhost:7296/api/music/album/{albumId}"); 


                if (response != null)
                {
                    
                    TracksList.ItemsSource = response;
                    AlbumAuthor.Text = response[0].Author.ToString();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки треков: {ex.Message}");
            }
        }


    }
}
