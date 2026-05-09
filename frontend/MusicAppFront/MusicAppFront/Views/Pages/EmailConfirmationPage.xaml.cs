using System;
using System.Collections.Generic;
using System.Linq;
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
using System.Windows.Threading;

namespace MusicAppFront.Views.Pages
{
    /// <summary>
    /// Логика взаимодействия для EmailConfirmationPage.xaml
    /// </summary>
    public partial class EmailConfirmationPage : Page
    {
        private DispatcherTimer _timer;
        private int _timeLeft = 59;
        public EmailConfirmationPage()
        {
            InitializeComponent();
            StartTimer();
        }
        private void StartTimer()
        {
            _timeLeft = 59;
            ResendButton.IsEnabled = false;
            TimerText.Visibility = Visibility.Visible;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_timeLeft > 0){
                _timeLeft--;
                TimerText.Text = $"Повторная отправка через 00:{_timeLeft:D2}";
            }
            else{
                _timer.Stop();
                TimerText.Visibility = Visibility.Collapsed; 
                ResendButton.IsEnabled = true;
            }
        }

        private void ResendButton_Click(object sender, RoutedEventArgs e)
        {
            //тут потом будет вызов метода бэка для повторной отправки
            StartTimer(); 
        }

        private void ConfirmCode_Click(object sender, RoutedEventArgs e)
        {
            // тут потом будет проверка кода через api

            var mainWindow = new Windows.MainWindow();
            mainWindow.Show();
            Window.GetWindow(this).Close();
        }
    }
}
