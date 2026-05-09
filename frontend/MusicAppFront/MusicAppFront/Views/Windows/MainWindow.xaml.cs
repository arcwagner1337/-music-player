using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicAppFront.Views.Pages;

namespace MusicAppFront.Views.Windows
{
    public partial class MainWindow : Window
    {
        private HomePage _homePage;
        private ProfilePage _profilePage;
        private FavoritesPage _favoritesPage;
        private PlaylistsPage _playlistsPage;
        private MaxFlowPage _maxFlowPage;
        public MainWindow()
        {
            InitializeComponent();

            _homePage = new HomePage();
            _profilePage = new ProfilePage();
            _favoritesPage = new FavoritesPage();
            _playlistsPage = new PlaylistsPage();
            _maxFlowPage = new MaxFlowPage();
            MainFrame.Navigate(_homePage);
        }
        private void HomeTab_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content != _homePage)
                MainFrame.Navigate(_homePage);
        }
        private void ProfileTab_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content != _profilePage)
                MainFrame.Navigate(_profilePage);
        }
        private void FavoritesTab_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content != _favoritesPage)
                MainFrame.Navigate(_favoritesPage);
        }
        private void PlaylistsTab_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content != _playlistsPage)
                MainFrame.Navigate(_playlistsPage);
        }
        private void MaxFlowTab_Click(object sender, RoutedEventArgs e)
        {
            if (MainFrame.Content != _maxFlowPage)
                MainFrame.Navigate(_maxFlowPage);
        }

        private void MainFrame_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as FrameworkElement;
            while (element != null){
                if (element is ContentControl cc && cc.Style == (Style)FindResource("PlaylistCardStyle")){
                    MainFrame.Navigate(new InfoPlaylistPage());
                    e.Handled = true;
                    break;
                }element = VisualTreeHelper.GetParent(element) as FrameworkElement;
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter){
                if (!string.IsNullOrWhiteSpace(SearchBox.Text)){
                    MainFrame.Navigate(new SearchPage());
                }
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        private void OpenFullPlayer_Click(object sender, MouseButtonEventArgs e)
        {
            MainFrame.Navigate(new FullPlayerPage());
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
