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

namespace MusicAppFront.Views.Pages
{
    /// <summary>
    /// Логика взаимодействия для HomePage.xaml
    /// </summary>
    public partial class HomePage : Page
    {
        private MainWindow _mainWindow;
        private testPlayer.NativePlayer _nativePlayer;
        public HomePage(MainWindow mainWindow, NativePlayer nativePlayer)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            HistoryItemsControl.ItemsSource = _mainWindow.HistoryList;
            _nativePlayer = nativePlayer;
        }

        private async void OnTrackClicked(object sender, RoutedEventArgs e)
        {
            _mainWindow.isAlbumOpenAndActive = false;
            var track = (sender as FrameworkElement).DataContext as testPlayer.NativePlayer.TrackWithStreamDto;

            _mainWindow.BottomTrackTitle.Text = "Резолв аудио...";

            
            track.StreamUrl = await _nativePlayer.ResolveAudioUrlAsync(track.YtUrl);
            track.IsResolved = true;

            if (string.IsNullOrEmpty(track.StreamUrl))
            {
                MessageBox.Show("Не удалось получить аудиопоток. Все сервера yt-dlp недоступны.");
                //btn.IsEnabled = true;
                return;
            }

           
            await _nativePlayer.PlayTrack(track, addToHistory: true, clearForward: true);
        }
    }
}
