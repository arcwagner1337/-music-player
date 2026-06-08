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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MusicAppFront.Views.Pages
{


    public partial class EmailConfirmationPage : Page
    {
        private readonly string _userEmail;

        private static readonly HttpClient _client = new HttpClient { BaseAddress = new Uri(App.Settings.BaseAddress), Timeout = TimeSpan.FromSeconds(10) };


        private DispatcherTimer _timer;
        private int _timeLeft = 59;
        public EmailConfirmationPage(string email)
        {
            InitializeComponent();
            _userEmail = email; 
            StartTimer();
        }
        private void StartTimer()
        {
            _timeLeft = 59;
            ResendButton.IsEnabled = false;
            TimerText.Visibility = Visibility.Visible;

            if (_timer != null) _timer.Stop();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_timeLeft > 0)
            {
                _timeLeft--;
                TimerText.Text = $"Повторная отправка через 00:{_timeLeft:D2}";
            }
            else
            {
                _timer.Stop();
                TimerText.Visibility = Visibility.Collapsed;
                ResendButton.IsEnabled = true;
            }
        }

        private async void ResendButton_Click(object sender, RoutedEventArgs e)
        {
            
            StartLoading(true);
            try
            {
                var response = await _client.PostAsJsonAsync("api/register/resend", new { email = _userEmail });
                if (response.IsSuccessStatusCode)
                {
                    StartTimer();
                }
            }
            catch { }
            finally
            {
                StartLoading(false);
            }

            
        }

        private async void ConfirmCode_Click(object sender, RoutedEventArgs e)
        {
            
            ConfirmErrorTextBlock.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(CodeInput.Text))
            {
                ShowConfirmError("Введите код подтверждения!");
                return;
            }
            StartLoading(true);

            try
            {
                var verifyData = new { email = _userEmail, code = CodeInput.Text };

              
                var response = await _client.PostAsJsonAsync("api/register/confirm", verifyData);

                if (response.IsSuccessStatusCode)
                {
                    

                    var data = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                    if (data.ContainsKey("token"))
                    {
                        AuthStorage.AuthStorage.SaveToken(data["token"]);
                    }

                    var mainWindow = new Windows.MainWindow();
                    mainWindow.Show();
                    Window.GetWindow(this).Close();
                }
                else
                {
                    
                    var errorData = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                    string errorMsg = errorData?.GetValueOrDefault("error") ?? "Неверный код";
                    ShowConfirmError(GetFriendlyConfirmError(errorMsg));
                }
            }
            catch (Exception)
            {
                ShowConfirmError("Сервер не отвечает. Проверь сеть.");
            }
            finally
            {
                StartLoading(false);
            }
        }

        private void StartLoading(bool isLoading)
        {
            ConfirmButton.IsEnabled = !isLoading;
            ConfirmButton.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;
            LoadingIcon.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;

            var sb = (Storyboard)this.Resources["RotateAnimation"];
            if (isLoading) sb.Begin(); else sb.Stop();
        }

        private void ShowConfirmError(string msg)
        {
            ConfirmErrorTextBlock.Text = msg;
            ConfirmErrorTextBlock.Visibility = Visibility.Visible;
        }

        private string GetFriendlyConfirmError(string code) => code switch
        {
            "Invalid or expired code" => "Код неверный или просрочен",
            "DB_ERROR" => "Ошибка базы данных",
            _ => "Не удалось подтвердить почту"
        };
    }
}
