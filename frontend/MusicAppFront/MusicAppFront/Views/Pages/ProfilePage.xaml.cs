using MusicAppFront.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
    /// <summary>
    /// Логика взаимодействия для ProfilePage.xaml
    /// </summary>
    public partial class ProfilePage : Page
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
        public ProfilePage()
        {
            InitializeComponent();
            LoadUserData();
        }
        private async void Logout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Сначала уведомляем сервер (опционально, но правильно)
                // Мы просто шлем пустой POST, сервер пришлет команду на удаление куки
                await _client.PostAsync("api/logout", null);
            }
            catch { /* Если сервер недоступен, просто игнорим и чистим локально */ }

            // 2. Чистим локальное хранилище (удаляем token.txt)
            AuthStorage.AuthStorage.Clear();

            // 3. Переходим на окно логина
            var loginWin = new Windows.LoginWindow();
            loginWin.Show();

            // 4. Закрываем текущее основное окно
            Window.GetWindow(this)?.Close();
        }

        private async void LoadUserData()
        {
            try
            {
                string token = AuthStorage.AuthStorage.GetToken();
                if (string.IsNullOrEmpty(token)) return;

                // Создаем запрос вручную, чтобы подкинуть заголовок
                var request = new HttpRequestMessage(HttpMethod.Get, "api/user/me");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var user = await response.Content.ReadFromJsonAsync<User>();
                    if (user != null)
                    {
                        // Ставим данные в XAML элементы (убедись, что дал им x:Name в ксамле)
                        UserName.Text = user.Username;
                        UserNameSmall.Text = user.Username;
                        UserEmail.Text = user.Email;
                        UserID.Text = user.Id.ToString();

                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Если токен "протух" — разлогиниваем
                    Logout_Click(null, null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка связи с сервером при загрузке профиля");
            }
        }

    }
}
