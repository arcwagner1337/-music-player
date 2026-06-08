using MusicAppFront.Views.Windows;
using System;
using System.Collections.Generic;
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

namespace MusicAppFront.Views.Pages
{

    public partial class CreatePlaylist : Page
    {
        private readonly MainWindow _mainWindow;
        private static readonly HttpClient _httpClient = new HttpClient
        {

            BaseAddress = new Uri(App.Settings.BaseAddress)


        };
        public CreatePlaylist(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            InitializeComponent();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private async void CreatePlaylist_Click(object sender, RoutedEventArgs e)
        {
            string playlistName = PlaylistNameTextBox.Text.Trim();

 
            if (string.IsNullOrEmpty(playlistName))
            {
                MessageBox.Show("Название плейлиста не может быть пустым!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }


            string currentUsername = MusicAppFront.Views.Windows.MainWindow._currentUserName;

            System.Diagnostics.Debug.WriteLine("currentUsername  " + currentUsername);


            var requestBody = new
            {
                PlaylistName = playlistName,
                Username = currentUsername
            };

            try
            {
            
                var response = await _httpClient.PostAsJsonAsync("api/music/create-playlist", requestBody);

                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Плейлист \"{playlistName}\" успешно создан!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

             
                    if (this.NavigationService != null && this.NavigationService.CanGoBack)
                    {
                        this.NavigationService.GoBack();
                    }
                }
                else
                {
  
                    var errorResult = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                    string message = errorResult != null && errorResult.ContainsKey("message")
                        ? errorResult["message"]
                        : "Не удалось создать плейлист.";

                    MessageBox.Show(message, "Ошибка сервера", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
   
                MessageBox.Show($"Ошибка подключения к серверу: {ex.Message}", "Ошибка сети", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


    }
}
