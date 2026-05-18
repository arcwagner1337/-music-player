using MusicAppFront.browserMusicPlayer;
using MusicAppFront.Models;
using MusicAppFront.Views.Pages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using static MusicAppFront.browserMusicPlayer.BrowserMusicPlayer;
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
        private BrowserMusicPlayer _browserMusicPlayer;

        private HttpClient _client = new HttpClient();




        public MainWindow()
        {
            InitializeComponent();

            _client = new HttpClient();
            _client.BaseAddress = new Uri("https://localhost:7296/");

            _homePage = new HomePage();
            _profilePage = new ProfilePage();
            _favoritesPage = new FavoritesPage();
            _playlistsPage = new PlaylistsPage();
            _maxFlowPage = new MaxFlowPage();
            MainFrame.Navigate(_homePage);
            _browserMusicPlayer = new BrowserMusicPlayer(this);
            _browserMusicPlayer.InitBrowser();
        }


        private async void GlobalPlayPauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_browserMusicPlayer._isPlayerReady) return;

            try
            {
                // 1. Проверяем, загружен ли вообще какой-либо медиапоток в видеоплеер браузера
                string currentSrc = await HiddenBrowser.ExecuteScriptAsync(
                    "document.querySelector('video') ? document.querySelector('video').currentSrc : ''"
                );

                // Убираем лишние кавычки, которые может вернуть ExecuteScriptAsync
                currentSrc = currentSrc?.Trim('"') ?? "";

                // 2. Если в плеере пусто и ничего не загружалось — кнопка ничего не делает (или можно включить дефолтный трек)
                if (string.IsNullOrEmpty(currentSrc) || currentSrc == "null")
                {
                    return;
                }

                // 3. Если трек загружен — дергаем play/pause в зависимости от текущего состояния
                if (_browserMusicPlayer._isPlaying)
                {
                    await HiddenBrowser.ExecuteScriptAsync("var v = document.querySelector('video'); if(v) v.pause();");
                }
                else
                {
                    await HiddenBrowser.ExecuteScriptAsync("var v = document.querySelector('video'); if(v) v.play();");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка глобального переключения Play/Pause: {ex.Message}");
            }
        }



        private void NextBtn_Click(object sender, RoutedEventArgs e)
        {
            _browserMusicPlayer.BtnNext_Click(sender, e);
        }

        private void PrevBtn_Click(object sender, RoutedEventArgs e)
        {
            _browserMusicPlayer.BtnPrev_Click(sender, e);
        }

        private void TimelineSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _browserMusicPlayer.TimelineSlider_PreviewMouseLeftButtonDown(sender, e); 
        }

        private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _browserMusicPlayer.TimelineSlider_ValueChanged(sender, e);

        }


        private void TimelineSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _browserMusicPlayer.TimelineSlider_DragStarted(sender, e);
        }

        private async void TimelineSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
           _browserMusicPlayer.TimelineSlider_DragCompleted(sender, e);
        }



        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {

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
                    var results = await _client.GetFromJsonAsync<SearchResultDto>(
                        $"api/music/search?query={Uri.EscapeDataString(SearchBox.Text)}"
                    );

                    if (results != null)
                    {
                        // Передаем результаты в конструктор страницы
                        MainFrame.Navigate(new SearchPage(results, this, _browserMusicPlayer));
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





        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
