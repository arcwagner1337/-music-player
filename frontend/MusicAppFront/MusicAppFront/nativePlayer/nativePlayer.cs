using LibVLCSharp.Shared;
using Microsoft.Web.WebView2;
using MusicAppFront;
using MusicAppFront.Models;
using MusicAppFront.Views.Pages;
using MusicAppFront.Views.Windows;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
//using NAudio.Wave;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static System.Net.Mime.MediaTypeNames;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using Path = System.IO.Path;

namespace testPlayer
{


    public class NativePlayer
    {
        private int _pushStream;
        private int _stream;
        private HttpClient _client = new HttpClient();
        private DispatcherTimer _timer;
        public bool _isDragging = false;
        private CancellationTokenSource _cts;
        private bool _isPlayerReady = false;
        private bool _isHttpLoading = false;
        private System.Windows.Threading.DispatcherTimer _syncTimer;
        private List<string> _globalHistory = new List<string>();
        public LibVLCSharp.Shared.LibVLC _libVlc;


        public LibVLCSharp.Shared.MediaPlayer _mediaPlayer;



        private string _currentArtist = "";
        private string _currentTrack = "";
        public MusicAppFront.Views.Pages.FullPlayerPage FullPlayerPage { get; set; }





    
        private ConcurrentQueue<TrackWithStreamDto> _playbackQueue = new ConcurrentQueue<TrackWithStreamDto>();
        public ConcurrentQueue<TrackWithStreamDto> _playbackAlbumQueue = new ConcurrentQueue<TrackWithStreamDto>();




        private Stack<TrackWithStreamDto> _historyStack = new Stack<TrackWithStreamDto>();
        private Stack<TrackWithStreamDto> _forwardStack = new Stack<TrackWithStreamDto>();


        public Stack<TrackWithStreamDto> _historyStackAlbum = new Stack<TrackWithStreamDto>();
        public Stack<TrackWithStreamDto> _forwardStackAlbum = new Stack<TrackWithStreamDto>();


        public TrackWithStreamDto _currentlyPlayingTrack;

  
        private CancellationTokenSource _preloadCts;

     
        public class TrackWithStreamDto
        {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string ImageUrl { get; set; }
            public string StreamUrl { get; set; }
            public double Duration { get; set; }
            public string YtUrl { get; set; }
            public bool IsResolved { get; set; }
            public bool IsResolvingProcess { get; set; }

        }

        public class TrackDto2
        {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string CoverImageUrl { get; set; }

            public string Url { get; set; }
            public string CleanArtist { get; set; }
            public string CleanTitle { get; set; }

       
            public TrackDto2() { }

            public TrackDto2(string title, string artist, string url, string cleanArtist, string cleanTitle, string coverImageUrl)
            {
                Title = title;
                Artist = artist;
                Url = url;
                CleanArtist = cleanArtist;
                CoverImageUrl = coverImageUrl;
            }
        }

        private bool _isPlaying = false;
        private SemaphoreSlim _ResolveSemaphore = new SemaphoreSlim(3);
        private Random _random = new Random();





        

        private readonly string[] _fastServers =
        {
            App.Settings.DlpServerUrlUnlog1,
            App.Settings.DlpServerUrlUnlog2
        };

      

        private readonly string[] _fallbackServers =
            {
                App.Settings.DlpServerUrlLog1,
                App.Settings.DlpServerUrlLog2
            };

        private int _fastIndex = 0;
        private int _fallbackIndex = 0;

        private object _serverLock = new object();
        private readonly MainWindow _mainWindow;


        public event Action<bool> PlayerStatusChanged;
        private static readonly string HistoryFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recent_tracks.json");
        private const int MaxTracks = 20;

        public NativePlayer(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;

            if (_libVlc != null)
            {
                _libVlc.Log += (sender, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[VLC Native] {e.Message}");
                };
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[VLC null]...");
            }

        }

        private string GetServerForAttempt(int attempt)
        {
            lock (_serverLock)
            {
         
                if (attempt < _fastServers.Length)
                {
                    var server = _fastServers[_fastIndex % _fastServers.Length];
                    _fastIndex++;
                    return server;
                }
                else 
                {
                    var server = _fallbackServers[_fallbackIndex % _fallbackServers.Length];
                    _fallbackIndex++;
                    return server;
                }
            }
        }

        public async Task AddToHistory(TrackWithStreamDto newTrack)
        {
            List<TrackWithStreamDto> history = new List<TrackWithStreamDto>();

            try
            {
                
                
                string json = await File.ReadAllTextAsync(HistoryFile);
               
                history = JsonConvert.DeserializeObject<List<TrackWithStreamDto>>(json) ?? new List<TrackWithStreamDto>();
           

                history.RemoveAll(t => t.YtUrl == newTrack.YtUrl);
                history.Insert(0, newTrack);

                if (history.Count > MaxTracks)
                {
                    history = history.Take(MaxTracks).ToList();
                }

             
                string newJson = JsonConvert.SerializeObject(history, Formatting.Indented);

             
                await File.WriteAllTextAsync(HistoryFile, newJson);
                System.Diagnostics.Debug.WriteLine($"История сохранена в: {HistoryFile}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ОШИБКА записи истории: {ex.Message}");
            }
        }


        public List<TrackWithStreamDto> GetHistory()
        {
            if (!File.Exists(HistoryFile)) return new List<TrackWithStreamDto>();

            try
            {
                string json = File.ReadAllText(HistoryFile);
                return JsonConvert.DeserializeObject<List<TrackWithStreamDto>>(json) ?? new List<TrackWithStreamDto>();
            }
            catch
            {
                return new List<TrackWithStreamDto>();
            }
        }


        private async Task PreloadRecommendationForAlbumsAsync(string artist, string track, CancellationToken token)
        {
            string currentArtist = artist;
            string currentTrack = track;

            while (!token.IsCancellationRequested && _playbackAlbumQueue.Count < 10)
            {
                try
                {
                    string currentTrackId = $"{currentArtist.ToLower()} - {currentTrack.ToLower()}";
                    if (!_globalHistory.Contains(currentTrackId)) _globalHistory.Add(currentTrackId);

          
                    var excludeList = _globalHistory.Skip(Math.Max(0, _globalHistory.Count - 40)).ToList();

         
                    if (_mainWindow?.GlobalAlbumResults?.Tracks != null)
                    {
                        foreach (var searchTrack in _mainWindow.GlobalAlbumResults.Tracks)
                        {
                            string searchTrackKey = $"{searchTrack.Author?.ToLower()} - {searchTrack.Title?.ToLower()}";
                            if (!excludeList.Contains(searchTrackKey))
                            {
                                excludeList.Add(searchTrackKey);
                            }
                        }
                    }

                    var body = new
                    {
                        artist = currentArtist,
                        track = currentTrack,
                        exclude = excludeList
                    };

                    var content = new StringContent(
                        Newtonsoft.Json.JsonConvert.SerializeObject(body),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );


                    System.Diagnostics.Debug.WriteLine($"[preload album] пошла очередь реков для альбома...");
                    string json = string.Empty;
                    using (var response = await _client.PostAsync(
                        $"{App.Settings.BaseAddress}api/music/GetNextRecommended", content, token))


                    {
                        response.EnsureSuccessStatusCode();
                        json = await response.Content.ReadAsStringAsync();
                    }

                    if (token.IsCancellationRequested) break;

                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                    if (data == null) break;
                    foreach (var item in data)
                    {
                        double.TryParse((string)item.duration, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out double dur);

                        var nextTrack = new TrackWithStreamDto
                        {
                            Artist = item.artist,
                            Title = item.title,
                            ImageUrl = (string)item.imageUrl,
                            YtUrl = (string)item.streamUrl,
                            Duration = dur,
                            IsResolved = false,

                        };

                        _playbackAlbumQueue.Enqueue(nextTrack);
                        System.Diagnostics.Debug.WriteLine($"[preload album] {nextTrack.Artist} - {nextTrack.Title}, очередь: {_playbackAlbumQueue.Count}");


                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                nextTrack.StreamUrl = await ResolveAudioUrlAsync(nextTrack.YtUrl);
                                nextTrack.IsResolved = true;
                                System.Diagnostics.Debug.WriteLine($"[preload album] резолв готов: {nextTrack.Artist} - {nextTrack.Title}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[preload album] Ошибка резолва ссылки YouTube: {ex.Message}");
                            }
                        });

                        currentArtist = nextTrack.Artist;
                        currentTrack = nextTrack.Title;
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[preload album] ошибка: {ex.Message}");

         
                    await Task.Delay(1000, token);
                }
            }
        }











        private async Task PreloadRecommendationsAsync(string artist, string track, CancellationToken token)
        {
            string currentArtist = artist;
            string currentTrack = track;

            while (!token.IsCancellationRequested && _playbackQueue.Count < 10)
            {
                try
                {
                    string currentTrackId = $"{currentArtist.ToLower()} - {currentTrack.ToLower()}";
                    if (!_globalHistory.Contains(currentTrackId)) _globalHistory.Add(currentTrackId);

                    var excludeList = _globalHistory.Skip(Math.Max(0, _globalHistory.Count - 40)).ToList();

                    if (_mainWindow?.GlobalResults?.Tracks != null)
                    {
                        foreach (var searchTrack in _mainWindow.GlobalResults.Tracks)
                        {
                            string searchTrackKey = $"{searchTrack.Author?.ToLower()} - {searchTrack.Title?.ToLower()}";
                            if (!excludeList.Contains(searchTrackKey))
                            {
                                excludeList.Add(searchTrackKey);
                            }
                        }
                    }

                    var body = new
                    {
                        artist = currentArtist,
                        track = currentTrack,
                        exclude = excludeList 
                    };

                    var content = new StringContent(
                        Newtonsoft.Json.JsonConvert.SerializeObject(body),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );

                    string json = string.Empty;
                    System.Diagnostics.Debug.WriteLine($"[preload] пошла очередь реков обычных...");
                    using (var response = await _client.PostAsync(

                        $"{App.Settings.BaseAddress}api/music/GetNextRecommended", content, token))


                    {
                        response.EnsureSuccessStatusCode();
                        json = await response.Content.ReadAsStringAsync();
                    }

                    if (token.IsCancellationRequested) break;

                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                    if (data == null) break;
                    foreach (var item in data)
                    {
                        double.TryParse((string)item.duration, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out double dur);

                        var nextTrack = new TrackWithStreamDto
                        {
                            Artist = item.artist,
                            Title = item.title,
                            ImageUrl = (string)item.imageUrl,
                            YtUrl = (string)item.streamUrl,
                            Duration = dur,
                            IsResolved = false
                        };

                        _playbackQueue.Enqueue(nextTrack);
                        System.Diagnostics.Debug.WriteLine($"[preload] {nextTrack.Artist} - {nextTrack.Title}, очередь: {_playbackQueue.Count}");


                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                nextTrack.StreamUrl = await ResolveAudioUrlAsync(nextTrack.YtUrl);
                                nextTrack.IsResolved = true;
                                System.Diagnostics.Debug.WriteLine($"[preload] резолв готов: {nextTrack.Artist} - {nextTrack.Title}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[preload] Ошибка резолва ссылки YouTube: {ex.Message}");
                            }
                        });

                        currentArtist = nextTrack.Artist;
                        currentTrack = nextTrack.Title;
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[preload] ошибка: {ex.Message}");

      
                    await Task.Delay(1000, token);
                }
            }
        }




        public async Task PlayTrack(TrackWithStreamDto track, bool addToHistory = true, bool clearForward = true)
        {
            if (track == null) return;

            if (addToHistory && _currentlyPlayingTrack != null)
                _historyStack.Push(_currentlyPlayingTrack);

            if (clearForward)
                _forwardStack.Clear();

            _currentlyPlayingTrack = track;
            System.Diagnostics.Debug.WriteLine("img url  " + track.ImageUrl);

            await _mainWindow.Dispatcher.InvokeAsync(() =>
            {
                _mainWindow.BottomTrackTitle.Text = $"{track.Artist} - {track.Title}";
                _mainWindow.TimelineSlider.Maximum = track.Duration;
                _mainWindow.TimelineSlider.Value = 0;
                _mainWindow.GlobalPlayPauseBtn.Content = "\uE103"; // Две палочки (Пауза)
                _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);


                if (FullPlayerPage != null)
                {
            
                    FullPlayerPage.BIG_TrackTitle.Text = track.Title;
                    FullPlayerPage.BIG_Author.Text = track.Artist;



    
                    FullPlayerPage.BIG_GlobalPlayPauseBtn.Content = "\uE103";
                    FullPlayerPage.BIG_GlobalPlayPauseBtn.Padding = new Thickness(0);

                    if (!string.IsNullOrEmpty(track.ImageUrl))
                    {
                        FullPlayerPage.BIG_TrackImage.Visibility = System.Windows.Visibility.Visible;
                        FullPlayerPage.BIG_TrackImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(track.ImageUrl));
                    }
                    else
                    {
                        FullPlayerPage.BIG_TrackImage.Source = _mainWindow.BottomTrackImage.Source;
                        FullPlayerPage.BIG_TrackImage.Visibility = System.Windows.Visibility.Collapsed;
                    }
                }


                if (!string.IsNullOrEmpty(track.ImageUrl))
                {
                    _mainWindow.BottomTrackImage.Visibility = System.Windows.Visibility.Visible;
                    _mainWindow.BottomTrackImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(track.ImageUrl));
                }
                else
                {
              
                    _mainWindow.BottomTrackImage.Visibility = System.Windows.Visibility.Collapsed;

                }
            });

            System.Diagnostics.Debug.WriteLine("track.ImageUrl  " + track.ImageUrl);

            _preloadCts?.Cancel();
            _preloadCts = new CancellationTokenSource();

            if (clearForward) 
            {
                while (_playbackQueue.TryDequeue(out _)) { }
            }

            if (_playbackQueue.Count < 10)
                _ = PreloadRecommendationsAsync(track.Artist, track.Title, _preloadCts.Token);
            System.Diagnostics.Debug.WriteLine($"[playtrack] StreamUrl: {track.StreamUrl?.Substring(0, Math.Min(60, track.StreamUrl?.Length ?? 0))}");

            await AddToHistory(track);
            PlayWithVlc(track.StreamUrl);
        }




        private async Task PreloadAlbumSurroundingsAsync()
        {

            System.Diagnostics.Debug.WriteLine("резолвинг для альбома пошел епта ");

            var forwardTracks = _forwardStackAlbum.ToList();
            var historyTracks = _historyStackAlbum.ToList();

            if (_playbackAlbumQueue.Count < 30)
                _ = PreloadRecommendationForAlbumsAsync(forwardTracks.Last().Artist, forwardTracks.Last().Title, _preloadCts.Token);

 
            foreach (var track in forwardTracks.Concat(historyTracks))
            {
              

              
                if ((track.IsResolved || track.IsResolvingProcess) && !string.IsNullOrEmpty(track.StreamUrl)) continue;
                track.IsResolvingProcess = true;
                try
                {
                  
                    if (string.IsNullOrEmpty(track.YtUrl))
                    {

                        string url = $"{App.Settings.BaseAddress}api/music/stream?artist={Uri.EscapeDataString(track.Artist)}&track={Uri.EscapeDataString(track.Title)}";


                        var json = await _client.GetStringAsync(url);
                        var data = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(json);

                        if (data != null && data.Count >= 2)
                        {
                            track.YtUrl = data[0];
                            double.TryParse(data[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dur);
                            track.Duration = dur;
                        }
                    }

  
                    if (!string.IsNullOrEmpty(track.YtUrl))
                    {

                        track.StreamUrl = await ResolveAudioUrlAsync(track.YtUrl);
                        track.IsResolved = true;

                        System.Diagnostics.Debug.WriteLine($"[background] закеширован: {track.Title}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[background] ошибка резолва {track.Title}: {ex.Message}");
                }
                finally
                {

                    track.IsResolvingProcess = false;
                }
            }


        }



        public async Task PlayAlbumTrack(TrackWithStreamDto track, bool addToHistory = true, bool clearForward = true)
        {
            if (track == null) return;


            if (addToHistory && _currentlyPlayingTrack != null)
            {
                _historyStackAlbum.Push(_currentlyPlayingTrack);

            }



            _currentlyPlayingTrack = track;

   
            await _mainWindow.Dispatcher.InvokeAsync(() =>
            {
                _mainWindow.BottomTrackTitle.Text = $"{track.Artist} - {track.Title}";
                _mainWindow.TimelineSlider.Maximum = track.Duration;
                _mainWindow.TimelineSlider.Value = 0;
                _mainWindow.GlobalPlayPauseBtn.Content = "\uE103"; // Две палочки (Пауза)
                _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);


                if (FullPlayerPage != null)
                {
        
                    FullPlayerPage.BIG_TrackTitle.Text = track.Title;
                    FullPlayerPage.BIG_Author.Text = track.Artist;

                    FullPlayerPage.BIG_GlobalPlayPauseBtn.Content = "\uE103";
                    FullPlayerPage.BIG_GlobalPlayPauseBtn.Padding = new Thickness(0);

        
                    if (!string.IsNullOrEmpty(track.ImageUrl))
                    {
                        FullPlayerPage.BIG_TrackImage.Visibility = System.Windows.Visibility.Visible;
                        FullPlayerPage.BIG_TrackImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(track.ImageUrl));
                    }
                    else
                    {
                        FullPlayerPage.BIG_TrackImage.Source = _mainWindow.BottomTrackImage.Source;
                        FullPlayerPage.BIG_TrackImage.Visibility = System.Windows.Visibility.Collapsed;
                    }
                }


                if (!string.IsNullOrEmpty(track.ImageUrl))
                {
                    _mainWindow.BottomTrackImage.Visibility = System.Windows.Visibility.Visible;
                    _mainWindow.BottomTrackImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(track.ImageUrl));
                }
                else
                {
  
                    _mainWindow.BottomTrackImage.Visibility = System.Windows.Visibility.Collapsed;

                }
            });


            _preloadCts?.Cancel();
            _preloadCts = new CancellationTokenSource();



            _ = PreloadAlbumSurroundingsAsync();



            PlayWithVlc(track.StreamUrl);
        }






        public void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

            if (_isDragging && _mainWindow.TotalTimeText != null & _mainWindow.CurrentTimeText != null)
            {


                _mainWindow.TotalTimeText.Text = $"{FormatTime(_mainWindow.TimelineSlider.Maximum)}";
                _mainWindow.CurrentTimeText.Text = $"{FormatTime(_mainWindow.TimelineSlider.Value)}";
            }

            if (_isDragging && FullPlayerPage != null && FullPlayerPage.BIG_TotalTime != null & FullPlayerPage.BIG_CurrentTime != null)
            {


                FullPlayerPage.BIG_TotalTime.Text = $"{FormatTime(_mainWindow.TimelineSlider.Maximum)}";
                FullPlayerPage.BIG_CurrentTime.Text = $"{FormatTime(_mainWindow.TimelineSlider.Value)}";
            }
        }




        public async Task<string> ResolveAudioUrlAsync(string youtubeUrl)
        {
            int totalServers = _fastServers.Length + _fallbackServers.Length;


            for (int attempt = 0; attempt < totalServers; attempt++)
            {
    
                string server = GetServerForAttempt(attempt);
                try
                {
                    string apiUrl = $"{server}/?url={Uri.EscapeDataString(youtubeUrl)}";
                    var sw = Stopwatch.StartNew();
                    string json = await _client.GetStringAsync(apiUrl);
                    sw.Stop();
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Чистое время ожидания ответа от Python-сервера: {sw.ElapsedMilliseconds} мс");
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                    string url = (string)data.url;

                    if (!string.IsNullOrEmpty(url))
                    {
                        System.Diagnostics.Debug.WriteLine($"[resolve] готово через {server}");
                        return url;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[resolve] {server} упал: {ex.Message}, пробуем следующий");
                }
            }

            System.Diagnostics.Debug.WriteLine("[resolve] все серваки упали");
            return null;
        }




        private void PlayWithVlc(string audioUrl)
        {
            if (string.IsNullOrEmpty(audioUrl))
            {
                System.Diagnostics.Debug.WriteLine("[vlc] пустой url");
                return;
            }

            if (_libVlc != null)
            {
                try
                {
              
                    _libVlc.Log -= VlcNativeLogHandler;
                }
                catch { }

          
                _libVlc.Log += VlcNativeLogHandler;
                System.Diagnostics.Debug.WriteLine("[vlc] Логирование LibVLC успешно активировано.");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[vlc] КРИТИЧЕСКАЯ ОШИБКА: _libVlc равен null даже в момент воспроизведения!");
            }

   
            string insecureUrl = audioUrl.Replace("https://", "http://");

            System.Diagnostics.Debug.WriteLine($"[vlc] обычный ссылк: {audioUrl}");


            System.Diagnostics.Debug.WriteLine($"[vlc] Включаем инсекьюрный поток: {insecureUrl}");

            _mediaPlayer.Stop();
            PlayerStatusChanged?.Invoke(false);

            _libVlc.SetUserAgent("com.google.android.apps.youtube.vr/1.0 (Linux; U; Android 9; en_US;)", "http");

            using (var media = new Media(_libVlc, insecureUrl, FromType.FromLocation))
            {
      
                media.AddOption("http-forward-cookies=0");
                media.AddOption("http-continuous");

                _mediaPlayer.Media = media;
            }


            _mediaPlayer.Play();
            PlayerStatusChanged?.Invoke(true);

        }

        private void VlcNativeLogHandler(object sender, LibVLCSharp.Shared.LogEventArgs e)
        {

            System.Diagnostics.Debug.WriteLine($"[VLC Native] [{e.Level}] {e.Message}");
        }
        public async void BtnPlay_Click(object sender, RoutedEventArgs e, SearchResultDto results)
        {

            foreach (var item in results.Tracks)
            {
                System.Diagnostics.Debug.WriteLine("GlobalAlbumResults:  " + item.Title);
            }



            foreach (var item in _forwardStackAlbum)
            {
                System.Diagnostics.Debug.WriteLine("_forwardStackAlbum:  " + item.Title);
            }

            foreach (var item in _historyStackAlbum)
            {
                System.Diagnostics.Debug.WriteLine("_historyStackAlbum:  " + item.Title);
            }
            foreach (var item in _playbackAlbumQueue)
            {
                System.Diagnostics.Debug.WriteLine("Album queue:  " + item.Title);
            }

            foreach (var item in _playbackQueue)
            {
                System.Diagnostics.Debug.WriteLine("queue:  " + item.Title);
            }


            //foreach(var item in _mainWindow.HistoryList)
            //{
            //    System.Diagnostics.Debug.WriteLine("HistoryList data:  " + item.Title);
            //}



            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Pause();
                _mainWindow.GlobalPlayPauseBtn.Content = "\uE102"; // Треугольник (Плей)
                _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(2, 0, 0, 0);

                if (FullPlayerPage != null)
                {
                    FullPlayerPage.BIG_GlobalPlayPauseBtn.Content = "\uE102"; // Треугольник (Плей)
                    FullPlayerPage.BIG_GlobalPlayPauseBtn.Padding = new Thickness(2, 0, 0, 0);
                }




                var currentPlaying = _currentlyPlayingTrack;
                if (currentPlaying != null && results.Tracks != null)
                {
                    foreach (var track in results.Tracks)
                    {
     
                        bool isMatch = string.Equals(track.Title?.Trim(), currentPlaying.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(track.Author?.Trim(), currentPlaying.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (isMatch)
                        {
                     
                            track.IsPlaying = false;
                            break; 
                        }
                    }
                }




                return;
            }

            if (_mediaPlayer.Media != null && !_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Play();
                PlayerStatusChanged?.Invoke(true);
                _mainWindow.GlobalPlayPauseBtn.Content = "\uE103"; // Две палочки (Пауза)
                _mainWindow.GlobalPlayPauseBtn.Padding = new Thickness(0);
                if (FullPlayerPage != null)
                {
                    FullPlayerPage.BIG_GlobalPlayPauseBtn.Content = "\uE103"; // Две палочки (Пауза)
                    FullPlayerPage.BIG_GlobalPlayPauseBtn.Padding = new Thickness(0);
                }


                var currentPlaying = _currentlyPlayingTrack;
                if (currentPlaying != null && results.Tracks != null)
                {
                    foreach (var track in results.Tracks)
                    {
               
                        bool isMatch = string.Equals(track.Title?.Trim(), currentPlaying.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(track.Author?.Trim(), currentPlaying.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (isMatch)
                        {
                          
                            track.IsPlaying = true;
                            break; 
                        }
                    }
                }

                return;
            }


        }






        public string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                return "00:00";

            TimeSpan t = TimeSpan.FromSeconds(seconds);

            return t.TotalHours >= 1
                ? t.ToString(@"hh\:mm\:ss")
                : t.ToString(@"mm\:ss");
        }




        public async void BtnPrevAlbum_Click(object sender, RoutedEventArgs e, SearchResultDto results)
        {
            if (_historyStackAlbum.Count == 0)
            {
             
                return;
            }

            try
            {
                //BtnPrev.IsEnabled = false;

                if (_currentlyPlayingTrack != null)
                    _forwardStackAlbum.Push(_currentlyPlayingTrack);

   
                var previousTrack = _historyStackAlbum.Pop();




                if (previousTrack != null && results.Tracks != null && _currentlyPlayingTrack != null)
                {
                    foreach (var track in results.Tracks)
                    {
          
                        bool isMatch = string.Equals(track.Title?.Trim(), previousTrack.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(track.Author?.Trim(), previousTrack.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (isMatch)
                        {
                          
                            track.IsPlaying = true;
                            break;
                        }
                    }

                    foreach (var track in results.Tracks)
                    {
                    
                        bool isMatch = string.Equals(track.Title?.Trim(), _currentlyPlayingTrack.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(track.Author?.Trim(), _currentlyPlayingTrack.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (isMatch)
                        {
                         
                            track.IsPlaying = false;

                            break; 
                        }
                    }


                }



                await PlayAlbumTrack(previousTrack, addToHistory: false, clearForward: false);
            }
            catch (Exception ex)
            {
                _mainWindow.BottomTrackTitle.Text = $"Ошибка назад: {ex.Message}";
            }
            finally
            {
             
            }
        }


        public async void BtnPrev_Click(object sender, RoutedEventArgs e, SearchResultDto results)
        {
            if (_historyStack.Count == 0)
            {
         
                return;
            }

            try
            {
                //BtnPrev.IsEnabled = false;

          
                if (_currentlyPlayingTrack != null)
                    _forwardStack.Push(_currentlyPlayingTrack);

      
                var previousTrack = _historyStack.Pop();

                if (previousTrack != null && results.Tracks != null)
                {
                    foreach (var track in results.Tracks)
                    {
             
                        bool isMatch = string.Equals(track.Title?.Trim(), previousTrack.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(track.Author?.Trim(), previousTrack.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (isMatch)
                        {
                           
                            track.IsPlaying = true;
                            break;
                        }
                    }
                }


      
                await PlayTrack(previousTrack, addToHistory: false, clearForward: false);
            }
            catch (Exception ex)
            {
                _mainWindow.BottomTrackTitle.Text = $"Ошибка назад: {ex.Message}";
            }
            finally
            {
             
            }
        }

        private bool _isSkippingAlbum = false;




        public async Task PlayNextAlbumTrackAsync(SearchResultDto results)
        {


            if (_isSkippingAlbum) return;
            _isSkippingAlbum = true;

            System.Diagnostics.Debug.WriteLine($"_forwardStackAlbum count: {_forwardStackAlbum.Count}");
            try
            {

                var currentPlaying = _currentlyPlayingTrack;
                if (currentPlaying != null && results.Tracks != null)
                {
                    foreach (var track in results.Tracks)
                    {
                    
                        bool isMatch = string.Equals(track.Title?.Trim(), currentPlaying.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(track.Author?.Trim(), currentPlaying.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (isMatch)
                        {
                            
                            track.IsPlaying = false;
                            break; 
                        }
                    }
                }


                if (_forwardStackAlbum.Count > 0)
                {
                    var nextFromHistory = _forwardStackAlbum.Pop();

                    if (nextFromHistory != null && results.Tracks != null)
                    {
                        foreach (var track in results.Tracks)
                        {
                          
                            bool isMatch = string.Equals(track.Title?.Trim(), nextFromHistory.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                        && string.Equals(track.Author?.Trim(), nextFromHistory.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                            if (isMatch)
                            {
                               
                                track.IsPlaying = true;
                                break; 
                            }

                        }
                    }


                    await PlayAlbumTrack(nextFromHistory, addToHistory: true, clearForward: false);
                    return;
                }

                // ПРИОРИТЕТ 2: очередь рекомендаций для альбома
                if (_playbackAlbumQueue.TryDequeue(out var next))
                {
                    _mediaPlayer.Stop();


                    await _mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _mainWindow.BottomTrackTitle.Text = $"{next.Artist} - {next.Title}";
                        _mainWindow.BottomTrackTitle.Text = next.IsResolved ? "now playing" : "buffering...";

                        if (FullPlayerPage != null)
                        {
                            FullPlayerPage.BIG_TrackTitle.Text = $"{next.Title}";
                            FullPlayerPage.BIG_Author.Text = $"{next.Artist}";

                            FullPlayerPage.BIG_TrackTitle.Text = next.IsResolved ? "now playing" : "buffering...";
                        }
                    });

                    if (!next.IsResolved)
                    {
                        //TxtStatus.Text = "buffering...";
                        var cts = new CancellationTokenSource(10000);
                        while (!next.IsResolved && !cts.Token.IsCancellationRequested) // ← был баг тут, ! пропущен
                        {
                            await Task.Delay(200);
                        }
                    }

                    await PlayAlbumTrack(next, clearForward: false);
                    return;
                }

                // ПРИОРИТЕТ 3: очередь пуста, экстренный поиск
                if (_currentlyPlayingTrack != null)
                {
                    _mainWindow.BottomTrackTitle.Text = "Ждём очередь...";
                    if (FullPlayerPage != null)
                    {
                        FullPlayerPage.BIG_TrackTitle.Text = "Ждём очередь...";
                    }
                    _mediaPlayer.Stop();

                    var cts = new CancellationTokenSource(30000); 
                    while (_playbackQueue.Count == 0 && !cts.Token.IsCancellationRequested)
                        await Task.Delay(200);

                    if (_playbackAlbumQueue.TryDequeue(out var waited))
                    {


                        await _mainWindow.Dispatcher.InvokeAsync(() =>
                        {
                            _mainWindow.BottomTrackTitle.Text = $"{waited.Artist} - {waited.Title}";
                            _mainWindow.BottomTrackTitle.Text = waited.IsResolved ? "now playing" : "buffering...";
                            if (FullPlayerPage != null)
                            {
                                FullPlayerPage.BIG_TrackTitle.Text = $"{waited.Title}";
                                FullPlayerPage.BIG_Author.Text = $"{waited.Artist}";

                                FullPlayerPage.BIG_TrackTitle.Text = waited.IsResolved ? "now playing" : "buffering...";
                            }

                            if (!string.IsNullOrEmpty(waited.ImageUrl))
                            {
                                _mainWindow.BottomTrackImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(waited.ImageUrl));
                                if (FullPlayerPage != null)
                                {
                                    FullPlayerPage.BIG_TrackImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(waited.ImageUrl));
                                }
                            }
                            else
                            {
                                if (FullPlayerPage != null)
                                {
                                    FullPlayerPage.BIG_TrackImage.Source = _mainWindow.BottomTrackImage.Source;
                                }
                            }
                        });

                        if (!waited.IsResolved)
                        {
                            var resolveCts = new CancellationTokenSource(30000);
                            while (!waited.IsResolved && !resolveCts.Token.IsCancellationRequested)
                                await Task.Delay(200);
                        }

                        await PlayAlbumTrack(waited, clearForward: false);
                    }
                    else
                    {
                        _mainWindow.BottomTrackTitle.Text = "Треки не найдены.";
                    }
                }
            }
            finally
            {
                _isSkippingAlbum = false;
            }
        }


        public async void BtnNextAlbum_Click(object sender, RoutedEventArgs e, SearchResultDto results)
        {


            var currentPlaying = _currentlyPlayingTrack;
            if (currentPlaying != null && results.Tracks != null)
            {
                foreach (var track in results.Tracks)
                {
                 
                    bool isMatch = string.Equals(track.Title?.Trim(), currentPlaying.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                && string.Equals(track.Author?.Trim(), currentPlaying.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                    if (isMatch)
                    {
                      
                        track.IsPlaying = false;
                        break; 
                    }
                }
            }
            try
            {
                //BtnNext.IsEnabled = false;
                await PlayNextAlbumTrackAsync(results);
            }
            catch (Exception ex)
            {
                _mainWindow.BottomTrackTitle.Text = $"Ошибка: {ex.Message}";
            }
            finally
            {
                //BtnNext.IsEnabled = true;
            }
        }









        private bool _isSkipping = false;



        public async Task PlayNextTrackAsync(SearchResultDto results)
        {


            if (_isSkipping) return;
            _isSkipping = true;


            try
            {
                // ПРИОРИТЕТ 1: история вперёд
                if (_forwardStack.Count > 0)
                {
                    var nextFromHistory = _forwardStack.Pop();

                    if (nextFromHistory != null && results.Tracks != null)
                    {
                        foreach (var track in results.Tracks)
                        {
                            
                            bool isMatch = string.Equals(track.Title?.Trim(), nextFromHistory.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                        && string.Equals(track.Author?.Trim(), nextFromHistory.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                            if (isMatch)
                            {
                                
                                track.IsPlaying = true;
                                break;
                            }
                        }
                    }


                    await PlayTrack(nextFromHistory, addToHistory: true, clearForward: false);
                    return;
                }

                // ПРИОРИТЕТ 2: очередь рекомендаций
                if (_playbackQueue.TryDequeue(out var next))
                {
                    _mediaPlayer.Stop();
                    PlayerStatusChanged?.Invoke(false);

                    await _mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _mainWindow.BottomTrackTitle.Text = $"{next.Artist} - {next.Title}";
                        _mainWindow.BottomTrackTitle.Text = next.IsResolved ? "now playing" : "buffering...";

                        if (FullPlayerPage != null)
                        {
                            FullPlayerPage.BIG_TrackTitle.Text = $"{next.Title}";
                            FullPlayerPage.BIG_Author.Text = $"{next.Artist}";

                            FullPlayerPage.BIG_TrackTitle.Text = next.IsResolved ? "now playing" : "buffering...";
                        }
                    });

                    if (!next.IsResolved)
                    {
                        //TxtStatus.Text = "buffering...";
                        var cts = new CancellationTokenSource(10000);
                        while (!next.IsResolved && !cts.Token.IsCancellationRequested) 
                        {
                            await Task.Delay(200);
                        }
                    }

                    await PlayTrack(next, clearForward: false);
                    return;
                }

                // ПРИОРИТЕТ 3: очередь пуста, экстренный поиск
                if (_currentlyPlayingTrack != null)
                {
                    _mainWindow.BottomTrackTitle.Text = "Ждём очередь...";
                    if (FullPlayerPage != null)
                    {
                        FullPlayerPage.BIG_TrackTitle.Text = "Ждём очередь...";
                    }
                    _mediaPlayer.Stop();
                    PlayerStatusChanged?.Invoke(false);
                    var cts = new CancellationTokenSource(30000); 
                    while (_playbackQueue.Count == 0 && !cts.Token.IsCancellationRequested)
                        await Task.Delay(200);

                    if (_playbackQueue.TryDequeue(out var waited))
                    {


                        await _mainWindow.Dispatcher.InvokeAsync(() =>
                        {
                            _mainWindow.BottomTrackTitle.Text = $"{waited.Artist} - {waited.Title}";
                            _mainWindow.BottomTrackTitle.Text = waited.IsResolved ? "now playing" : "buffering...";
                            if (FullPlayerPage != null)
                            {
                                FullPlayerPage.BIG_TrackTitle.Text = $"{waited.Title}";
                                FullPlayerPage.BIG_Author.Text = $"{waited.Artist}";

                                FullPlayerPage.BIG_TrackTitle.Text = waited.IsResolved ? "now playing" : "buffering...";
                            }

                            if (!string.IsNullOrEmpty(waited.ImageUrl))
                            {
                                _mainWindow.BottomTrackImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(waited.ImageUrl));
                                if (FullPlayerPage != null)
                                {
                                    FullPlayerPage.BIG_TrackImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(waited.ImageUrl));
                                }
                            }
                            else
                            {
                                if (FullPlayerPage != null)
                                {
                                    FullPlayerPage.BIG_TrackImage.Source = _mainWindow.BottomTrackImage.Source;
                                }
                            }
                        });

                        if (!waited.IsResolved)
                        {
                            var resolveCts = new CancellationTokenSource(30000);
                            while (!waited.IsResolved && !resolveCts.Token.IsCancellationRequested)
                                await Task.Delay(200);
                        }

                        await PlayTrack(waited, clearForward: false);
                    }
                    else
                    {
                        _mainWindow.BottomTrackTitle.Text = "Треки не найдены.";
                    }
                }
            }
            finally
            {
                _isSkipping = false;
            }
        }






        public async void BtnNext_Click(object sender, RoutedEventArgs e, SearchResultDto results)
        {


            var currentPlaying = _currentlyPlayingTrack;
            if (currentPlaying != null && results.Tracks != null)
            {
                foreach (var track in results.Tracks)
                {
                 
                    bool isMatch = string.Equals(track.Title?.Trim(), currentPlaying.Title?.Trim(), StringComparison.OrdinalIgnoreCase)
                                && string.Equals(track.Author?.Trim(), currentPlaying.Artist?.Trim(), StringComparison.OrdinalIgnoreCase);

                    if (isMatch)
                    {
                       
                        track.IsPlaying = false;
                        break; 
                    }
                }
            }
            try
            {
               
                await PlayNextTrackAsync(results);
            }
            catch (Exception ex)
            {
                _mainWindow.BottomTrackTitle.Text = $"Ошибка: {ex.Message}";
            }
            finally
            {
           
            }
        }



        public void TimelineSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
           
            _isDragging = true;

           
            if (FullPlayerPage != null)
            {
                FullPlayerPage._isDraggingBigSlider = true;
            }
        }

        public async void TimelineSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            var slider = sender as System.Windows.Controls.Slider;
            if (slider != null)
            {
                
                _mediaPlayer.Time = (long)(slider.Value * 1000);
            }

          
            _isDragging = false;
            if (FullPlayerPage != null)
            {
                FullPlayerPage._isDraggingBigSlider = false;
            }
        }



        public async void TimelineSlider_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

            if (e.OriginalSource is System.Windows.Controls.Primitives.Thumb)
                return;

            var slider = (System.Windows.Controls.Slider)sender;

      
            System.Windows.Point clickPoint = e.GetPosition(slider);

     
            double relativePosition = clickPoint.X / slider.ActualWidth;


            relativePosition = Math.Max(0.0, Math.Min(1.0, relativePosition));

      
            double newValue = slider.Minimum + (relativePosition * (slider.Maximum - slider.Minimum));

            _isDragging = true;

            if (FullPlayerPage != null)
            {
                FullPlayerPage._isDraggingBigSlider = true;
            }
            _mediaPlayer.Time = (long)(newValue * 1000);
            _isDragging = false;
            if (FullPlayerPage != null)
            {
                FullPlayerPage._isDraggingBigSlider = false;
            }

  
            _isDragging = false;
        }



       



    }
}
