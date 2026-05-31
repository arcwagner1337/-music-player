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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using MusicAppFront.AuthStorage;
using MusicAppFront.Views.Windows;

namespace MusicAppFront.Views.Pages
{

    public partial class LoginPage : Page
    {
        //public static string currentUserName = "";
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
        public LoginPage()
        {
            InitializeComponent();
        }
        private void GoToRegister_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new RegisterPage());
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = new Windows.MainWindow();
            mainWindow.Show();
            Window.GetWindow(this).Close();


            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folderPath = System.IO.Path.Combine(appDataPath, "MusicApp");
            string filePath = System.IO.Path.Combine(folderPath, "token.txt");

            var loginBtn = (Button)sender;
            ErrorTextBlock.Visibility = Visibility.Collapsed;
            ErrorTextBlock.Text = "";

            if (string.IsNullOrWhiteSpace(UsernameInput.Text) || string.IsNullOrWhiteSpace(PasswordInput.Password))
            {
                ShowError("Введите логин и пароль");
                return;
            }
            loginBtn.IsEnabled = false;
            ButtonText.Visibility = Visibility.Collapsed;
            LoadingIcon.Visibility = Visibility.Visible;
            Storyboard sb = (Storyboard)this.Resources["RotateAnimation"];
            sb.Begin();

            try
            {

                var loginData = new
                {
                    Username = UsernameInput.Text,
                    Password = PasswordInput.Password
                };


                var response = await _client.PostAsJsonAsync("api/login", loginData);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                    if (data.ContainsKey("token"))
                    {
                        AuthStorage.AuthStorage.SaveToken(data["token"]); // Сохраняем без хардкода путей
                    }
                    //System.Diagnostics.Debug.WriteLine("data[\"username\"]:  " + data["username"]);
                    //currentUserName = data["username"];

                    //var mainWindow = new Windows.MainWindow();
                    mainWindow.Show();
                    Window.GetWindow(this).Close();
                }
                else
                {

                    var errorResponse = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                    string errorCode = errorResponse?.GetValueOrDefault("error") ?? "UNKNOWN_ERROR";
                    ShowError(GetFriendlyErrorMessage(errorCode));
                }
            }
            catch (Exception ex)
            {
                ShowError("Нет связи с сервером. Проверь бэкенд!");
            }
            finally
            {
                sb.Stop();
                LoadingIcon.Visibility = Visibility.Collapsed;
                ButtonText.Visibility = Visibility.Visible;
                loginBtn.IsEnabled = true;
            }
        }

        private string GetFriendlyErrorMessage(string errorCode)
        {
            return errorCode switch
            {
                "USER_NOT_FOUND" => "Пользователь с таким именем не найден.",
                "WRONG_PASSWORD" => "Неверный пароль. Попробуйте еще раз.",
                _ => "Произошла непредвиденная ошибка."
            };
        }
        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }



    }
}
