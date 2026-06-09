using backendxd.DTOS;
using backendxd.Models;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Playlists;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;



namespace backendxd.Services
{


    public class MusicService2
    {
        private readonly YoutubeClient _yt = new YoutubeClient();


        public async Task<object> SmartSearchAsync2(string query)
        {
            using var client = new HttpClient();

            var dzResponse = await client.GetFromJsonAsync<JsonElement>($"https://api.deezer.com/search?q={Uri.EscapeDataString(query)}&limit=20");

            var artists = new List<ArtistDto>();
            var tracks = new List<TrackDto2>();
            var topAlbums = new List<AlbumDto>();

            if (dzResponse.TryGetProperty("data", out var data))
            {

                var items = data.EnumerateArray().ToList();


                var uniqueArtists = items
                    .Select(item => item.GetProperty("artist"))
                    .GroupBy(a => a.GetProperty("id").GetInt64())
                    .Select(g => g.First())
                    .Take(3);

                var sortedArtists = uniqueArtists.OrderByDescending(a =>
                (a.GetProperty("name").GetString() ?? string.Empty).Equals(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0);

                foreach (var dzArtist in sortedArtists)
                {
                    string artistName = dzArtist.GetProperty("name").GetString() ?? string.Empty;

                    artists.Add(new ArtistDto(
                        artistName,
                        "",
                        dzArtist.GetProperty("picture_xl").GetString() ?? string.Empty,
                        "Биография загружается...",
                        dzArtist.GetProperty("id").GetInt64().ToString()
                    ));
                }

                var sortedItems = items.OrderByDescending(item =>
                    (item.GetProperty("artist").GetProperty("name").GetString() ?? string.Empty) 
                    .Equals(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0 
                    );


                foreach (var item in sortedItems)
                {
                    tracks.Add(new TrackDto2(
                        item.GetProperty("title").GetString() ?? string.Empty,
                        item.GetProperty("artist").GetProperty("name").GetString() ?? string.Empty,
                        "",
                        item.GetProperty("artist").GetProperty("name").GetString() ?? string.Empty,
                        item.GetProperty("title").GetString() ?? string.Empty,
                        item.GetProperty("album").GetProperty("cover_big").GetString() ?? string.Empty
                    ));
                }


                var uniqueAlbums = items
                    .GroupBy(x => x.GetProperty("album").GetProperty("id").GetInt64());


                foreach (var albGroup in uniqueAlbums)
                {
                    var alb = albGroup.First().GetProperty("album");
                    topAlbums.Add(new AlbumDto(
                        alb.GetProperty("title").GetString() ?? string.Empty,
                        alb.GetProperty("cover_xl").GetString() ?? string.Empty,
                        alb.GetProperty("id").GetInt64().ToString() ?? string.Empty,
                        "",
                        0
                    ));
                }
            }

            bool isArtistSearch = artists.Any(a => a.Name.Equals(query, StringComparison.OrdinalIgnoreCase));


            if (isArtistSearch)
            {
                artists = artists.OrderByDescending(a => a.Name.Equals(query, StringComparison.OrdinalIgnoreCase)).ToList();
                return new SearchResultDtoPreferArtists(artists, tracks, topAlbums);
            }
            else
            {
                tracks = tracks.OrderByDescending(t => t.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
                return new SearchResultDtoPreferTracks(tracks, topAlbums, artists);
            }

        }

        public async Task<List<TrackDto2>> GetAlbumTracksAsync(long albumId)
        {
            using var client = new HttpClient();

            var response = await client.GetFromJsonAsync<JsonElement>($"https://api.deezer.com/album/{albumId}");

            var tracks = new List<TrackDto2>();

            if (response.TryGetProperty("tracks", out var tracksProp))
            {
                var data = tracksProp.GetProperty("data").EnumerateArray();
                string artistName = response.GetProperty("artist").GetProperty("name").GetString() ?? string.Empty;
                string albumCover = response.GetProperty("cover_big").GetString() ?? string.Empty;

                foreach (var item in data)
                {
                    tracks.Add(new TrackDto2(
                        item.GetProperty("title").GetString() ?? string.Empty,
                        artistName,
                        "",
                        artistName,
                        item.GetProperty("title").GetString() ?? string.Empty,
                        albumCover
                    ));
                }
            }

            return tracks;
        }


        public async Task<List<AlbumDto>> GetArtistAlbumsAsync(long artistId)
        {
            using var client = new HttpClient();

            var response = await client.GetFromJsonAsync<JsonElement>($"https://api.deezer.com/artist/{artistId}/albums");

            var albums = new List<AlbumDto>();

            if (response.TryGetProperty("data", out var data))
            {
                foreach (var alb in data.EnumerateArray())
                {
                    albums.Add(new AlbumDto(
                        alb.GetProperty("title").GetString() ?? string.Empty,
                        alb.GetProperty("cover_xl").GetString() ?? string.Empty,
                        alb.GetProperty("id").GetInt64().ToString() ?? string.Empty,
                        "",
                        0
                    ));
                }
            }

            return albums;
        }





        public async Task<TrackDto2?> SearchOnYouTubeAsync(string artist, string track)
        {

            var searchQuery = $"\"{artist}\" {track} official audio";
            var searchResult = await _yt.Search.GetVideosAsync(searchQuery).CollectAsync(1);

            if (searchResult.Count > 0)
            {

                string[] stopWords = { "live", "concert", "remix", "cover", "festival", "tour" };

                var video = searchResult.OrderByDescending(v =>
                {

                    int score = 0;

                    if (v.Author.ChannelTitle.Equals($"{artist} - Topic", StringComparison.OrdinalIgnoreCase)) score += 10;

                    if (v.Title.Contains("Official Audio", StringComparison.OrdinalIgnoreCase)) score += 5;
                    return score;
                }).FirstOrDefault(v =>
                    v.Duration > TimeSpan.FromMinutes(1) &&
                    v.Duration < TimeSpan.FromMinutes(15) &&

                    !stopWords.Any(word => v.Title.Contains(word, StringComparison.OrdinalIgnoreCase)) &&

                    (v.Author.ChannelTitle.Contains(artist, StringComparison.OrdinalIgnoreCase) ||
                     v.Title.Contains(artist, StringComparison.OrdinalIgnoreCase))
                ) ?? searchResult.FirstOrDefault();

                if (video != null)
                {
                    string imageUrl = video.Thumbnails.OrderByDescending(t => t.Resolution.Width).First().Url;

                    return new TrackDto2(
                        video.Title,
                        video.Author.ChannelTitle,
                        video.Url,
                        artist,
                        track,
                        imageUrl
                    );
                }
            }
            return null;
        }


        public async Task<List<string?>> SearchOnYouTubeAsync3(string artist, string track)
        {

            var searchQuery = $"\"{artist}\" {track}";


            var searchResults = _yt.Search.GetVideosAsync(searchQuery);
            var videoList = new List<YoutubeExplode.Search.VideoSearchResult>();

            int count = 0;
            await foreach (var videoResult in searchResults)
            {
                videoList.Add(videoResult);
                count++;
                if (count >= 5) break;
            }

            if (videoList.Count > 0)
            {
                string[] stopWords = { "live", "concert", "remix", "cover", "festival", "tour" };

                var bestVideo = videoList.OrderByDescending(v =>
                {
                    int score = 0;
                    string titleLower = v.Title.ToLowerInvariant();
                    string channelLower = v.Author.ChannelTitle.ToLowerInvariant();
                    string artistLower = artist.ToLowerInvariant();

                    if (channelLower.Equals($"{artistLower} - topic", StringComparison.OrdinalIgnoreCase)) score += 50;
                    if (channelLower.Contains(artistLower)) score += 20;


                    if (titleLower.Contains("official audio")) score += 30;
                    if (titleLower.Contains("official video")) score += 25;
                    if (titleLower.Contains("remaster")) score += 15;

                    if (stopWords.Any(word => titleLower.Contains(word))) score -= 200;


                    if (titleLower.Contains("/") || titleLower.Contains(" / ")) score -= 150;


                    double durationMinutes = v.Duration?.TotalMinutes ?? 0;


                    if (durationMinutes > 6.0) score -= 100;
                    if (durationMinutes < 1.5) score -= 80;

                    return score;
                }).FirstOrDefault();

                var video = bestVideo ?? videoList.FirstOrDefault();

                if (video != null)
                {
                    double seconds = video.Duration?.TotalSeconds ?? 0;
                    string durationStr = seconds.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
                    string musicUrl = video.Url.Replace("www", "music");

                    return new List<string?> { musicUrl, durationStr };
                }
            }
            return null!;
        }


        public async Task<List<string?>> SearchOnYouTubeAsync2(string artist, string track)
        {

            var searchQuery = $"\"{artist}\" {track} official audio";


            var searchResults = _yt.Search.GetVideosAsync(searchQuery);
            var videoList = new List<YoutubeExplode.Search.VideoSearchResult>();


            int count = 0;
            await foreach (var videoResult in searchResults)
            {
                videoList.Add(videoResult);
                count++;


                if (count >= 3) break;
            }

            if (videoList.Count > 0)
            {
                string[] stopWords = { "live", "concert", "remix", "cover", "festival", "tour" };

                var video = videoList.OrderByDescending(v =>
                {
                    int score = 0;
                    if (v.Author.ChannelTitle.Equals($"{artist} - Topic", StringComparison.OrdinalIgnoreCase)) score += 10;
                    if (v.Title.Contains("Official Audio", StringComparison.OrdinalIgnoreCase)) score += 5;
                    return score;
                }).FirstOrDefault(v =>
                    v.Duration > TimeSpan.FromMinutes(1) &&
                    v.Duration < TimeSpan.FromMinutes(15) &&
                    !stopWords.Any(word => v.Title.Contains(word, StringComparison.OrdinalIgnoreCase)) &&
                    (v.Author.ChannelTitle.Contains(artist, StringComparison.OrdinalIgnoreCase) || v.Title.Contains(artist, StringComparison.OrdinalIgnoreCase))
                ) ?? videoList.FirstOrDefault();

                if (video != null)
                {

                    double seconds = video.Duration?.TotalSeconds ?? 0;


                    string durationStr = seconds.ToString("G", System.Globalization.CultureInfo.InvariantCulture);

                    List<string?> Data = new List<string?>
                        {
                            video.Url,
                            durationStr
                        };

                    return Data;
                }
            }

            return null!;
        }



        private static readonly Dictionary<string, (string url, DateTime expiry)> _urlCache = new();

        public async Task<string> GetCachedDirectUrlAsync(string videoUrl)
        {

            if (_urlCache.TryGetValue(videoUrl, out var cached) && cached.expiry > DateTime.Now)
            {
                Console.WriteLine("[CACHE] Используем сохраненную ссылку");
                return cached.url;
            }


            var ytInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp.exe",
                Arguments = $"-g -f bestaudio \"{videoUrl}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var ytProcess = Process.Start(ytInfo);

            if (ytProcess?.StandardOutput == null)
            {
                Console.WriteLine("[ERROR] Не удалось запустить yt-dlp или перенаправить вывод");
                return string.Empty;
            }

            
            string directUrl = (await ytProcess.StandardOutput.ReadToEndAsync()).Trim();

            _urlCache[videoUrl] = (directUrl, DateTime.Now.AddHours(2));

            return directUrl;

            //string directUrl = (await ytProcess.StandardOutput.ReadToEndAsync()).Trim();


            //_urlCache[videoUrl] = (directUrl, DateTime.Now.AddHours(2));

            //return directUrl;
        }

        public Process GetFFmpegAudioProcess(string directUrl, int seekSeconds = 0)
        {
            string seekTime = TimeSpan.FromSeconds(seekSeconds).ToString(@"hh\:mm\:ss");
            var ffmpegInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg.exe",
                //Arguments = $"-ss {seekTime} -i \"{directUrl}\" -avoid_negative_ts make_zero -acodec pcm_s16le -f s16le -ar 44100 -ac 2 -loglevel quiet -",
                Arguments = $"-ss {seekTime} -i \"{directUrl}\" -acodec pcm_s16le -f s16le -ar 44100 -ac 2 -loglevel quiet -",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(ffmpegInfo);

            if (process == null)
            {
                throw new InvalidOperationException("Не удалось запустить процесс ffmpeg.exe. Проверьте наличие утилиты.");
            }

            return process;

            //return Process.Start(ffmpegInfo);
        }













        public async Task<string> GetFullStreamByTrackInfoAsync(string artist, string track)
        {
            try
            {

                var ytTrack = await SearchOnYouTubeAsync(artist, track);

                if (ytTrack == null || string.IsNullOrEmpty(ytTrack.Url))
                    return string.Empty;


                return await GetAudioStreamUrl(ytTrack.Url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Ошибка при получении потока: {ex.Message}");
                return string.Empty;
            }
        }



        public async Task<string> GetAudioStreamUrl(string videoId)
        {


            var manifest = await _yt.Videos.Streams.GetManifestAsync(videoId);


            var streamInfo = manifest.GetAudioOnlyStreams()
                .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.Mp4)
                .GetWithHighestBitrate();

            if (streamInfo == null)
                streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

            return streamInfo.Url;
        }








        public async Task<List<TrackDto2>> GetSimilarTracksBatchAsync(string artist, string track, List<string> exclude, int batchSize = 15)
        {

            string apiKey = AppSettings.LastFmApiKey;

            string workerUrl2 = AppSettings.WorkerUrl2;


            using var client = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true });

            try
            {
                string rawLastFmUrl = $"https://ws.audioscrobbler.com/2.0/?method=track.getsimilar&artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(track)}&api_key={apiKey}&format=json&limit=100";
                string finalUrl2 = $"{workerUrl2}?url={Uri.EscapeDataString(rawLastFmUrl)}";

                var response = await client.GetFromJsonAsync<JsonElement>(finalUrl2);

                if (response.TryGetProperty("similartracks", out var similarTracks))
                {
                    var trackList = similarTracks.GetProperty("track").EnumerateArray()
                      .Where(t =>
                      {
                          string? art = t.TryGetProperty("artist", out var artEl) && artEl.TryGetProperty("name", out var artNameEl) ? artNameEl.GetString() : null;
                          string? name = t.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;

                          if (art == null || name == null) return false;

                          string key = $"{art.ToLower()} - {name.ToLower()}";
                          return !exclude.Contains(key);
                      })
                      .ToList();

                    if (trackList.Count > 0)
                    {

                        var selectedBatch = trackList.Take(batchSize).ToList();

                        var deezerTasks = selectedBatch.Select(async selected =>
                        {
                            string nextArtist = selected.GetProperty("artist").GetProperty("name").GetString() ?? string.Empty;
                            string nextTrack = selected.GetProperty("name").GetString() ?? string.Empty;
                            string imageUrl = "";

                            try
                            {
                                string dzSearchUrl = $"https://api.deezer.com/search?q=artist:\"{Uri.EscapeDataString(nextArtist)}\" track:\"{Uri.EscapeDataString(nextTrack)}\"&limit=1";
                                var dzResponse = await client.GetFromJsonAsync<JsonElement>(dzSearchUrl);
                                if (dzResponse.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                                {
                                    var firstMatch = data.EnumerateArray().First();
                                    imageUrl = firstMatch.GetProperty("album").GetProperty("cover_big").GetString() ?? string.Empty;
                                }
                            }
                            catch { }

                            return new TrackDto2(nextTrack, nextArtist, "", nextArtist, nextTrack, imageUrl);
                        });


                        var results = await Task.WhenAll(deezerTasks);
                        return results.ToList();
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"Ошибка рекомендаций: {ex.Message}"); }

            return new List<TrackDto2>();
        }




        public async Task<List<TrackDto2>> GetTopTracksByArtistBatchAsync(string artist, List<string> exclude, int batchSize = 15)
        {

            string rawUrl = $"https://ws.audioscrobbler.com/2.0/?method=artist.gettoptracks&artist={Uri.EscapeDataString(artist)}&api_key={AppSettings.LastFmApiKey}&format=json&limit=50";

            return await FetchAndParseLastFm(rawUrl, "toptracks", "track", exclude, batchSize);
        }


        public async Task<List<TrackDto2>> GetGlobalTrendingBatchAsync(List<string> exclude, int batchSize = 15)
        {

            string rawUrl = $"https://ws.audioscrobbler.com/2.0/?method=chart.gettoptracks&api_key={AppSettings.LastFmApiKey}&format=json&limit=50";

            return await FetchAndParseLastFm(rawUrl, "tracks", "track", exclude, batchSize);
        }


        private async Task<List<TrackDto2>> FetchAndParseLastFm(string rawUrl, string rootProp, string listProp, List<string> exclude, int batchSize)
        {

            string workerUrl2 = AppSettings.WorkerUrl2;

            using var client = new HttpClient();
            string finalUrl = $"{workerUrl2}?url={Uri.EscapeDataString(rawUrl)}";

            try
            {
                var response = await client.GetFromJsonAsync<JsonElement>(finalUrl);
                if (response.TryGetProperty(rootProp, out var root))
                {
                    var tracks = root.GetProperty(listProp).EnumerateArray()
                        .Where(t =>
                        {
                            string art = t.TryGetProperty("artist", out var a) && a.TryGetProperty("name", out var n) ? (n.GetString() ?? string.Empty) : "";
                            string name = t.TryGetProperty("name", out var nm) ? (nm.GetString() ?? string.Empty) : "";
                            return !string.IsNullOrEmpty(art) && !exclude.Contains($"{art.ToLower()} - {name.ToLower()}");
                        })
                        .Take(batchSize);


                    return tracks.Select(t => new TrackDto2(t.GetProperty("name").GetString() ?? string.Empty, t.GetProperty("artist").GetProperty("name").GetString() ?? string.Empty, "", "", "", "")).ToList();
                }
            }
            catch { }
            return new List<TrackDto2>();
        }







    }


}
