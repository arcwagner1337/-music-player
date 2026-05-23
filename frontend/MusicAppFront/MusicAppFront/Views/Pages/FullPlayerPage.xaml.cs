using MusicAppFront.Models;
using MusicAppFront.Views.Windows;
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
using testPlayer;
using static testPlayer.NativePlayer;

namespace MusicAppFront.Views.Pages
{
    /// <summary>
    /// Логика взаимодействия для FullPlayerPage.xaml
    /// </summary>
    public partial class FullPlayerPage : Page
    {

        private readonly MainWindow _mainWindow;
        private readonly testPlayer.NativePlayer _nativePlayer;
        private readonly SearchResultDto _GlobalResults;


        public bool _isDraggingBigSlider = false;

        // Конструктор теперь принимает готовые объекты снаружи
        public FullPlayerPage(MainWindow mainWindow, testPlayer.NativePlayer player, SearchResultDto GlobalResults)
        {
            InitializeComponent();

            _mainWindow = mainWindow;
            _nativePlayer = player;
            _GlobalResults = GlobalResults;

            UpdateUiFromCurrentTrack();

            if (_nativePlayer != null)
            {
                _nativePlayer.FullPlayerPage = this;


                _nativePlayer._mediaPlayer.TimeChanged += (s, e) =>
                {
                    // Используем легковесный BeginInvoke с фоновым приоритетом, чтобы слайдер не лагал
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        double currentTime = e.Time / 1000.0; // VLC отдаёт миллисекунды

                        if (_mainWindow != null)
                        {
                            // ЖЕСТКАЯ СИНХРОНИЗАЦИЯ: копируем точный Максимум из мелкого слайдера при каждом тике
                            BIG_Slider.Maximum = _mainWindow.TimelineSlider.Maximum;
                        }

                        // Проверяем локальный флаг драггинга большого слайдера
                        if (!_isDraggingBigSlider && currentTime >= 0)
                        {
                            BIG_Slider.Value = currentTime;

                            // Заливаем текст, отформатированный через твой метод FormatTime
                            BIG_CurrentTime.Text = $"{_nativePlayer.FormatTime(BIG_Slider.Value)}";
                            BIG_TotalTime.Text = $"{_nativePlayer.FormatTime(BIG_Slider.Maximum)}";
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                };
            }

        }

        public void UpdateUiFromCurrentTrack()
        {
            // Проверяем, что плеер и трек вообще существуют
            //if (_nativePlayer == null || _nativePlayer._currentlyPlayingTrack == null) return;
            if (_nativePlayer == null) return;

            if (_nativePlayer._currentlyPlayingTrack == null)
            {
                BIG_TrackTitle.Text = _mainWindow.BottomTrackTitle.Text;
                BIG_Author.Text = "";
                BIG_TrackImage.Source = _mainWindow.BottomTrackImage.Source;
            }
            if (_nativePlayer._currentlyPlayingTrack != null)
            {

                var track = _nativePlayer._currentlyPlayingTrack;

                // В лоб заполняем все элементы интерфейса большого плеера
                BIG_TrackTitle.Text = track.Title;
                BIG_Author.Text = track.Artist;

                // Если внизу играет — ставим две палочки (паузу), иначе плей
                if (_nativePlayer._mediaPlayer.IsPlaying)
                {
                    BIG_GlobalPlayPauseBtn.Content = "\uE103";
                    BIG_GlobalPlayPauseBtn.Padding = new Thickness(0);
                }
                else
                {
                    BIG_GlobalPlayPauseBtn.Content = "\uE102";
                    BIG_GlobalPlayPauseBtn.Padding = new Thickness(2, 0, 0, 0);
                }

                if (!string.IsNullOrEmpty(track.ImageUrl))
                {
                    BIG_TrackImage.Source = _mainWindow.BottomTrackImage.Source; ;
                }
            }
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


        private void BIG_GlobalPlayPauseBtn_Click(object sender, RoutedEventArgs e)
        {
            _nativePlayer.BtnPlay_Click(sender, e, _GlobalResults);

        }



        private void BIG_NextBtn_Click(object sender, RoutedEventArgs e)
        {
            _nativePlayer.BtnNext_Click(sender, e, _GlobalResults);

        }

        private void BIG_PrevBtn_Click(object sender, RoutedEventArgs e)
        {
            _nativePlayer.BtnPrev_Click(sender, e, _GlobalResults);

        }

        private void BIG_TimelineSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _nativePlayer.TimelineSlider_PreviewMouseLeftButtonDown(sender, e);
        }

        private void BIG_TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _nativePlayer.TimelineSlider_ValueChanged(sender, e);

        }


        private void BIG_TimelineSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _nativePlayer.TimelineSlider_DragStarted(sender, e);
        }

        private async void BIG_TimelineSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            _nativePlayer.TimelineSlider_DragCompleted(sender, e);
        }

    }
}
