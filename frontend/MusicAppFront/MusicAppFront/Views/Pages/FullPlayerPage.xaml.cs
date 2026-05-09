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

namespace MusicAppFront.Views.Pages
{
    /// <summary>
    /// Логика взаимодействия для FullPlayerPage.xaml
    /// </summary>
    public partial class FullPlayerPage : Page
    {
        public FullPlayerPage()
        {
            InitializeComponent();
        }
        private void CloseFullPlayer_Click(object sender, RoutedEventArgs e)
        {
            if (this.NavigationService != null && this.NavigationService.CanGoBack)
            {
                this.NavigationService.GoBack();
            }
            else
            {
                if (this.NavigationService != null)
                {
                    this.NavigationService.Content = null;
                }
            }
        }
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}
