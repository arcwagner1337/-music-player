using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MusicAppFront.Models
{
    public class SearchResultDto
    {
        public class TrackDto2 : INotifyPropertyChanged
        {
            // Стандартные свойства для данных
            public string Title { get; set; }
            public string Author { get; set; }
            public string Url { get; set; }
            public string ImageUrl { get; set; }
            public string CleanTitle { get; set; }

            // Свойства с уведомлением об изменении (для UI)
            private bool _isPlaying;
            public bool IsPlaying
            {
                get => _isPlaying;
                set
                {
                    _isPlaying = value;
                    OnPropertyChanged(nameof(IsPlaying));
                }
            }

            private string _durationStr = "--:--";
            public string DurationStr
            {
                get => _durationStr;
                set
                {
                    _durationStr = value;
                    OnPropertyChanged(nameof(DurationStr));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


            private bool _isFavorite;
            public bool IsFavorite
            {
                get => _isFavorite;
                set
                {
                    _isFavorite = value;
                    OnPropertyChanged("IsFavorite");
                    // ТУТ ВЫЗЫВАЕМ МЕТОД СОХРАНЕНИЯ
                    Task.Run(() => ToggleFavoriteAsync());
                }
            }
            private static HttpClient _httpClient = new HttpClient { BaseAddress = new Uri("https://localhost:7296/") };
            public static string CurrentUsername = "asdqwe";
            private async Task ToggleFavoriteAsync()
            {

                var payload = new
                {
                    Username = CurrentUsername,
                    Title = this.Title,
                    Author = this.Author,
                    ImageUrl = this.ImageUrl
                };

                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                await _httpClient.PostAsync("api/music/toggle", content);
                // Вся логика API лежит прямо тут, внутри объекта трека!
                // Используй статический HttpClient или передавай его сюда.
                System.Diagnostics.Debug.WriteLine($"API Call: {Title} is now {IsFavorite}");
            }

            public void SetFavoriteSilently(bool value)
            {
                _isFavorite = value;
                OnPropertyChanged(nameof(IsFavorite));
            }
        }


        public record ArtistDto(string Name, string Url, string ImageUrl, string Bio, string Id);
        public record AlbumDto(
            string Name,
            string ImageUrl,
            string Id,
            string Url,
            int? Playcount
            );

        public List<ArtistDto> Artists { get; set; } = new();
        public List<TrackDto2> Tracks { get; set; } = new();


        [JsonPropertyName("topAlbums")]
        public List<AlbumDto> Albums { get; set; } = new();
    }
}
