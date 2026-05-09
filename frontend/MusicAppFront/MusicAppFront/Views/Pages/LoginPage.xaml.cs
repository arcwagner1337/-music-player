using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http;
using System.Net.Http.Json;
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

namespace MusicAppFront.Views.Pages
{
   
    public partial class LoginPage : Page
    {
        private static readonly HttpClient _client = new HttpClient { BaseAddress = new Uri("https://localhost:7296/") };
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
                    
                    var mainWindow = new Windows.MainWindow();
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
