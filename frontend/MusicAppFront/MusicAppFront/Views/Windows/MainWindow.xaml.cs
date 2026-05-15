using MusicAppFront.Models;
using MusicAppFront.Views.Pages;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
            GlobalPlayer.OnPlayingStarted += () =>
            {
                
                Dispatcher.Invoke(() =>
                {
                    GlobalPlayPauseBtn.Content = "\uE103";
                    GlobalPlayPauseBtn.Padding = new Thickness(0);
                });
            };

            GlobalPlayer.OnPlayingPaused += () =>
            {

                Dispatcher.Invoke(() =>
                {
                    GlobalPlayPauseBtn.Content = "\uE102";
                    GlobalPlayPauseBtn.Padding = new Thickness(2, 0, 0, 0);
                });
            };

            GlobalPlayer.OnTrackChanged += () => {
                var track = GlobalPlayer.CurrentTrack;
                if (track != null)
                {
                    Dispatcher.Invoke(() => {
                        BottomTrackTitle.Text = track.Title;
                        BottomTrackArtist.Text = track.Author;

                        // Обновляем обложку
                        if (!string.IsNullOrEmpty(track.ImageUrl))
                        {
                            try
                            {
                                BottomTrackImage.Source = new BitmapImage(new Uri(track.ImageUrl));
                                BottomTrackImage.Visibility = Visibility.Visible;
                                //BottomTrackPlaceholder.Visibility = Visibility.Collapsed;
                            }
                            catch
                            {
                                // Если ссылка битая или формат странный
                                ShowPlaceholder();
                            }
                        }
                        else
                        {
                            ShowPlaceholder();
                        }
                    });
                }
            };




            long currentTotalMs = 0; // Запомним текущую длину трека

            GlobalPlayer.OnLengthChanged += (totalMs) => {
                currentTotalMs = totalMs;
                var time = TimeSpan.FromMilliseconds(totalMs);
                Dispatcher.Invoke(() => {
                    TotalTimeText.Text = string.Format("{0}:{1:D2}", (int)time.TotalMinutes, time.Seconds);
                    TimelineSlider.Maximum = totalMs;
                });
            };

            GlobalPlayer.OnTimeChanged += (currentMs) => {
                var time = TimeSpan.FromMilliseconds(currentMs);
                Dispatcher.Invoke(() => {
                    CurrentTimeText.Text = string.Format("{0}:{1:D2}", (int)time.TotalMinutes, time.Seconds);
                    if (!TimelineSlider.IsMouseCaptureWithin) // Чтобы ползунок не прыгал, когда мы его тащим
                    {
                        TimelineSlider.Value = currentMs;
                    }
                });
            };
        }

        private void ShowPlaceholder()
        {
            BottomTrackImage.Visibility = Visibility.Collapsed;
            //BottomTrackPlaceholder.Visibility = Visibility.Visible;
        }

        private void TimelineSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (TimelineSlider.Maximum > 0)
            {
                // Рассчитываем относительную позицию (от 0.0 до 1.0)
                float seekPos = (float)(TimelineSlider.Value / TimelineSlider.Maximum);

                // Отправляем в VLC
                GlobalPlayer.Seek(seekPos);

                Debug.WriteLine($"[VLC] Перемотка на: {seekPos * 100}%");
            }
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            // 1. Проверяем, открыта ли сейчас страница альбома
            if (MainFrame.Content is InfoPlaylistPage albumPage)
            {
                // Вызываем метод в InfoPlaylistPage, который найдет следующий трек в списке
                await albumPage.PlayNextTrack();
            }
            else
            {
                // 2. Если мы в поиске, вызываем твой алгоритм подбора похожего трека
                //PlayRecommendedTrack();
            }
        }

        private void GlobalPlayPause_Click(object sender, RoutedEventArgs e)
        {
            //if (GlobalPlayer.CurrentTrack == null) return;

            GlobalPlayer.TogglePause();

            // Ручной переключатель: если в контенте треугольник — ставим палочки, и наоборот
            
            if (GlobalPlayPauseBtn.Content.Equals("\uE102"))
            {
                GlobalPlayPauseBtn.Content = "\uE103";
                GlobalPlayPauseBtn.Padding = new Thickness(0);
            }
            else
            {
                GlobalPlayPauseBtn.Content = "\uE102";
                GlobalPlayPauseBtn.Padding = new Thickness(2, 0, 0, 0);
            }
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





        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
