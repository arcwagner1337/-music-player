using MusicAppFront.Models;
using MusicAppFront.Views.Pages;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static MusicAppFront.Models.SearchResultDto;

namespace MusicAppFront.Views.Windows
{
    public partial class MainWindow : Window
    {
        private HomePage _homePage;
        private ProfilePage _profilePage;
        private FavoritesPage _favoritesPage;
        private PlaylistsPage _playlistsPage;
        private MaxFlowPage _maxFlowPage;

        private readonly HttpClient _httpClient;
        private MediaPlayer mediaPlayer = new MediaPlayer();

        public MainWindow()
        {
            InitializeComponent();

            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://localhost:7296/");

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
            while (element != null)
            {
                // Проверяем, что это наша карточка
                if (element is ContentControl cc && cc.Style == (Style)FindResource("PlaylistCardStyle"))
                {
                    // Достаем данные альбома из DataContext этой карточки
                    if (cc.DataContext is AlbumDto album)
                    {
                        // Передаем альбом в конструктор страницы
                        MainFrame.Navigate(new InfoPlaylistPage(album));
                    }
                    else
                    {
                        // Если данных нет, просто открываем (как было), 
                        // но лучше проверить, почему DataContext пустой
                        MainFrame.Navigate(new InfoPlaylistPage(null));
                    }

                    e.Handled = true;
                    break;
                }
                element = VisualTreeHelper.GetParent(element) as FrameworkElement;
            }
        }

        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                try
                {
                    // Делаем запрос. Используем GetFromJsonAsync, он сам десериализует ответ
                    // Если бэк требует токен, добавим заголовок (как обсуждали раньше)
                    var results = await _httpClient.GetFromJsonAsync<SearchResultDto>(
                        $"api/music/search?query={Uri.EscapeDataString(SearchBox.Text)}"
                    );

                    if (results != null)
                    {
                        // Передаем результаты в конструктор страницы
                        MainFrame.Navigate(new SearchPage(results));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка поиска: {ex.Message}");
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

        public void PlayMusic(string url)
        {
            // Очистка
            mediaPlayer.Stop();
            mediaPlayer.Close();

            // Подписки
            mediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
            mediaPlayer.MediaFailed -= MediaPlayer_MediaFailed;
            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
            mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;

            // Ссылка
            Uri mediaUri = new Uri(url);
            System.Diagnostics.Debug.WriteLine($"[PLAYER] Загрузка: {mediaUri.AbsoluteUri}");

            mediaPlayer.Open(mediaUri);

            // ВАЖНО: Дай ему команду играть ПРЯМО СЕЙЧАС
            mediaPlayer.Play();

            // Таймер-проверка (через 2 секунды спросим его: "ты как?")
            Task.Delay(2000).ContinueWith(t => {
                Application.Current.Dispatcher.Invoke(() => {
                    System.Diagnostics.Debug.WriteLine($"[PLAYER CHECK] Position: {mediaPlayer.Position} | Buffering: {mediaPlayer.BufferingProgress}");
                });
            });
        }

        private void MediaPlayer_MediaOpened(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[PLAYER SUCCESS] Файл успешно открыт! Начинаю воспроизведение.");
            System.Diagnostics.Debug.WriteLine($"[PLAYER INFO] Длительность: {mediaPlayer.NaturalDuration}");
        }

        private void MediaPlayer_MediaFailed(object sender, ExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[PLAYER ERROR] Ошибка: {e.ErrorException.Message}");
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
