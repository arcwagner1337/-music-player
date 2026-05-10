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

namespace MusicAppFront.Views.Pages
{
    /// <summary>
    /// Логика взаимодействия для SearchPage.xaml
    /// </summary>
    public partial class SearchPage : Page
    {
        private static readonly CookieContainer _cookieContainer = new CookieContainer();
        private static readonly HttpClient _client = new HttpClient(new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = true
        })
        {
            BaseAddress = new Uri("https://localhost:7296/"),
            Timeout = TimeSpan.FromSeconds(40)
        };

        public SearchPage(SearchResultDto results)
        {
            InitializeComponent();
            ArtistsList.ItemsSource = results.Artists;
            TracksList.ItemsSource = results.Tracks;
            AlbumsList.ItemsSource = results.Albums;
        }

        private async void TrackRow_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var track = btn?.DataContext as dynamic;
            if (btn == null || track == null) return;

            System.Diagnostics.Debug.WriteLine("не тыкай!");
            var button = sender as Button;
            // Убедись, что в твоем TrackDto2/TrackDto поля называются Author и Title
            // судя по твоему XAML биндингу: {Binding Title} и {Binding Author}
            //dynamic track = button.DataContext;

            try
            {
                btn.IsEnabled = false;
                string artist = track.Author;
                string title = track.Title;

                // 1. Стучимся на бэк за ссылкой
                // Используем Uri.EscapeDataString, чтобы пробелы в названиях не сломали URL
                string requestUrl = $"https://localhost:7296/api/music/stream?artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(title)}";

                var response = await _client.GetFromJsonAsync<StreamResponse>(requestUrl);
                System.Diagnostics.Debug.WriteLine($"[BACKEND RESPONSE]: {response?.Url}");




                if (response != null && !string.IsNullOrEmpty(response.Url))
                {
                    // 2. Запускаем воспроизведение
                    // MediaPlayer лучше держать глобально в MainWindow
                    


                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.PlayMusic(response.Url);
                    }

                    // 3. Обновляем UI нижнего плеера
                    //UpdateBottomBar(track);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка стриминга: {ex.Message}");
            }
            finally
            {
                btn.IsEnabled = true;
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
