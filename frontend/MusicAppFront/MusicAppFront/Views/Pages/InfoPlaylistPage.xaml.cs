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
        public InfoPlaylistPage(AlbumDto album)
        {
            InitializeComponent();

            this.DataContext = album;
            LoadTracks(album.Id);
        }

        private async void LoadTracks(string albumId)
        {
            try
            {
                // Стучимся в ваш новый эндпоинт
                // Не забудьте поменять URL на ваш реальный адрес бэкенда
                //var response = await _client.GetFromJsonAsync<List<TrackDto2>>($"https://your-api.com/api/music/album/{albumId}");
                var response = await _client.GetFromJsonAsync<List<TrackDto2>>($"https://localhost:7296/api/music/album/{albumId}"); 


                if (response != null)
                {
                    // Привязываем полученные треки к списку на странице
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
