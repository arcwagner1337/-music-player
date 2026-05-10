using MusicAppFront.Views.Pages;
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
using System.Windows.Shapes;

namespace MusicAppFront.Views.Windows
{
    /// <summary>
    /// Логика взаимодействия для LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();

            var token = MusicAppFront.AuthStorage.AuthStorage.GetToken();

            if (!string.IsNullOrEmpty(token))
            {
                // Если токен есть, сразу открываем главное окно
                var mainWindow = new MainWindow();
                mainWindow.Show();

                // Закрываем это окно логина, чтобы оно не висело в памяти
                this.Close();
            }
            else
            {
                // Токена нет — грузим страницу логина во фрейм
                AuthFrame.Navigate(new LoginPage());
            }
            
        }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
