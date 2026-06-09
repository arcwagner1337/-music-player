using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
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
using MusicAppFront.AuthStorage;

namespace MusicAppFront.Views.Pages
{

    public partial class RegisterPage : Page
    {
        private static readonly CookieContainer _cookieContainer = new CookieContainer();
        private static readonly HttpClient _client = new HttpClient(new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = true
        })
        {

            BaseAddress = new Uri(App.Settings.BaseAddress),


            Timeout = TimeSpan.FromSeconds(10)
        };
        public RegisterPage()
        {
            InitializeComponent();
        }

        private async void CreateAccount_Click(object sender, RoutedEventArgs e)
        {


            RegErrorTextBlock.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(RegUsernameInput.Text) ||
            string.IsNullOrWhiteSpace(RegPasswordInput.Password) || string.IsNullOrWhiteSpace(RegEmailInput.Text))
            {
                ShowRegError("Заполни все поля!");
                return;
            }

            if (!IsValidEmail(RegEmailInput.Text))
            {
                ShowRegError("Введи корректный адрес почты!");
                return;
            }

            StartLoading(true);


            try
            {
                var regData = new
                {
                    Username = RegUsernameInput.Text,
                    Email = RegEmailInput.Text,
                    Password = RegPasswordInput.Password
                };


                var response = await _client.PostAsJsonAsync("api/register/request", regData);

                if (response.IsSuccessStatusCode)
                {

                    this.NavigationService?.Navigate(new EmailConfirmationPage(regData.Email));
                }
                else
                {
                    var errorData = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                    ShowRegError(GetFriendlyRegError(errorData?.GetValueOrDefault("error")));
                }
            }
            catch (Exception)
            {
                ShowRegError("Сервер прилег отдохнуть...");
            }
            finally
            {
                StartLoading(false);
            }
        }

        private void GoBack_Click(object sender, RoutedEventArgs e)
        {
            if (this.NavigationService.CanGoBack)
            {
                this.NavigationService.GoBack();
            }
            else
            {
                this.NavigationService.Navigate(new LoginPage());
            }
        }

        private void StartLoading(bool isLoading)
        {
            RegisterButton.IsEnabled = !isLoading;
            RegButtonText.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;
            RegLoadingIcon.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;

            var sb = (Storyboard)this.Resources["RotateAnimation"];
            if (isLoading) sb.Begin(); else sb.Stop();
        }

        private void ShowRegError(string msg)
        {
            RegErrorTextBlock.Text = msg;
            RegErrorTextBlock.Visibility = Visibility.Visible;
        }

        private string GetFriendlyRegError(string code) => code switch
        {
            "USER_ALREADY_EXISTS" => "Этот ник уже занят",
            "EMAIL_ALREADY_EXISTS" => "Почта уже используется",
            _ => "Ошибка при регистрации"
        };

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;

            string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase);
        }


    }
}
