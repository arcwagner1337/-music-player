using MusicAppFront.Models;
using MusicAppFront.Views.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
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
    /// Логика взаимодействия для SearchPage.xaml
    /// </summary>
    public partial class SearchPage : Page
    {


        public SearchPage(SearchResultDto results)
        {
            InitializeComponent();
            ArtistsList.ItemsSource = results.Artists;
            TracksList.ItemsSource = results.Tracks;
            AlbumsList.ItemsSource = results.Albums;
            GlobalPlayer.Init();
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

            try
            {
                btn.IsEnabled = false;
                _isDataLoading = true;
                if (_lastPlayedTrack != null) _lastPlayedTrack.IsPlaying = false;


     
                string artist = track.Author;
                string title = track.Title;


                string streamUrl = $"https://localhost:7296/api/music/stream?artist={artist}&track={title}";
                string test = $"https://localhost:7296/api/music/stream?artist=judas%20priest&track=creatures";
                GlobalPlayer.CurrentTrack = track;

                System.Diagnostics.Debug.WriteLine("Отправляю в плеер: " + test);
                System.Diagnostics.Debug.WriteLine("ДИНАМИЧЕСКИЙ URL: " + streamUrl);

                var tcs = new TaskCompletionSource<bool>();
                GlobalPlayer.OnPlayingStarted += () => tcs.TrySetResult(true);

                // Теперь UriFormatException пропадет
                GlobalPlayer.Play(streamUrl);

                await Task.WhenAny(tcs.Task, Task.Delay(10000));
                track.IsPlaying = true;
                _lastPlayedTrack = track;

            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] Запрос отменен, так как выбран другой трек.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка стриминга: {ex.Message}");
            }
            finally
            {
                btn.IsEnabled = true; 
                _isDataLoading = false;
            }
        }

        // Вспомогательный класс для десериализации
        public class StreamResponse
        {
            [JsonPropertyName("url")]
            public string Url { get; set; }
        }

    }
}
