using backendxd.DTOS;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
                a.GetProperty("name").GetString().Equals(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0);

                foreach (var dzArtist in sortedArtists)
                {
                    string artistName = dzArtist.GetProperty("name").GetString();

                    artists.Add(new ArtistDto(
                        artistName,
                        "",
                        dzArtist.GetProperty("picture_xl").GetString(),
                        "Биография загружается...",
                        dzArtist.GetProperty("id").GetInt64().ToString()
                    ));
                }

                var sortedItems = items.OrderByDescending(item =>
                    item.GetProperty("artist").GetProperty("name").GetString()
                    .Equals(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0
                    );


                foreach (var item in sortedItems)
                {
                    tracks.Add(new TrackDto2(
                        item.GetProperty("title").GetString(),
                        item.GetProperty("artist").GetProperty("name").GetString(),
                        "",
                        item.GetProperty("artist").GetProperty("name").GetString(),
                        item.GetProperty("title").GetString(),
                        item.GetProperty("album").GetProperty("cover_big").GetString()
                    ));
                }


                var uniqueAlbums = items
                    .GroupBy(x => x.GetProperty("album").GetProperty("id").GetInt64());


                foreach (var albGroup in uniqueAlbums)
                {
                    var alb = albGroup.First().GetProperty("album");
                    topAlbums.Add(new AlbumDto(
                        alb.GetProperty("title").GetString(),
                        alb.GetProperty("cover_xl").GetString(),
                        alb.GetProperty("id").GetInt64().ToString(),
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
                string artistName = response.GetProperty("artist").GetProperty("name").GetString();
                string albumCover = response.GetProperty("cover_big").GetString();

                foreach (var item in data)
                {
                    tracks.Add(new TrackDto2(
                        item.GetProperty("title").GetString(),
                        artistName,
                        "",
                        artistName,
                        item.GetProperty("title").GetString(),
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
                        alb.GetProperty("title").GetString(),
                        alb.GetProperty("cover_xl").GetString(),
                        alb.GetProperty("id").GetInt64().ToString(),
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
                    // Проверка на стоп-слова: если в названии есть "live", скипаем
                    !stopWords.Any(word => v.Title.Contains(word, StringComparison.OrdinalIgnoreCase)) &&
                    // Убеждаемся, что это всё еще нужный нам артист
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
            // 1. Убираем жесткий маркер "official audio", чтобы не отсекать клипы "official video"
            // Оставляем кавычки на артисте для точности
            var searchQuery = $"\"{artist}\" {track}";

            // Берем первые 5 результатов для более надежного анализа
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

                // 2. Умное ранжирование с системой штрафов и поощрений
                var bestVideo = videoList.OrderByDescending(v =>
                {
                    int score = 0;
                    string titleLower = v.Title.ToLowerInvariant();
                    string channelLower = v.Author.ChannelTitle.ToLowerInvariant();
                    string artistLower = artist.ToLowerInvariant();

                    // ПЛЮСЫ: Приоритет официальным топикам артиста (там всегда чистый трек)
                    if (channelLower.Equals($"{artistLower} - topic", StringComparison.OrdinalIgnoreCase)) score += 50;
                    if (channelLower.Contains(artistLower)) score += 20;

                    // ПЛЮСЫ: Проверка маркеров официальных релизов
                    if (titleLower.Contains("official audio")) score += 30;
                    if (titleLower.Contains("official video")) score += 25; // Теперь клипы тоже в игре!
                    if (titleLower.Contains("remaster")) score += 15;

                    // ШТРАФЫ: Жестко топим концертники, каверы и ремиксы, если их не искали специально
                    if (stopWords.Any(word => titleLower.Contains(word))) score -= 200;

                    // ШТРАФЫ: Наказываем сдвоенные треки со слэшем (как ваш Iron Maiden)
                    if (titleLower.Contains("/") || titleLower.Contains(" / ")) score -= 150;

                    // ШТРАФЫ ЗА ДЛИТЕЛЬНОСТЬ (Аномально длинные видео)
                    double durationMinutes = v.Duration?.TotalMinutes ?? 0;

                    // Большинство синглов длятся от 3 до 5 минут. 
                    // Если видео идет дольше 6 минут или меньше 2 минут — это скорее всего склейка или тизер.
                    if (durationMinutes > 6.0) score -= 100;
                    if (durationMinutes < 1.5) score -= 80;

                    return score;
                }).FirstOrDefault();

                // Если даже после сортировки ничего не нашли (маловероятно), берем первый элемент
                var video = bestVideo ?? videoList.FirstOrDefault();

                if (video != null)
                {
                    double seconds = video.Duration?.TotalSeconds ?? 0;
                    string durationStr = seconds.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
                    string musicUrl = video.Url.Replace("www", "music");

                    return new List<string?> { musicUrl, durationStr };
                }
            }
            return null;
        }


        public async Task<List<string?>> SearchOnYouTubeAsync2(string artist, string track)
        {
            // Оставляем твою отличную логику поискового запроса
            var searchQuery = $"\"{artist}\" {track} official audio";

            // ОПТИМИЗАЦИЯ: Вместо .CollectAsync() используем асинхронный стрим.
            // Берем только первые 3 результата для быстрой фильтрации, а не всю страницу.
            var searchResults = _yt.Search.GetVideosAsync(searchQuery);
            var videoList = new List<YoutubeExplode.Search.VideoSearchResult>();

            // 2. Вручную берем первые 3 элемента через обычный счетчик
            int count = 0;
            await foreach (var videoResult in searchResults)
            {
                videoList.Add(videoResult);
                count++;

                // Как только набрали 3 штуки — мгновенно прерываем запрос к YouTube
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
                ) ?? videoList.FirstOrDefault(); // Если никто не подошел под фильтр, берем самый первый

                if (video != null)
                {
                    // Возвращаем прямую ссылку на видео для твоего WebView
                    double seconds = video.Duration?.TotalSeconds ?? 0;

                    // Переводим double в строку. Спецификатор "G" и InvariantCulture 
                    // гарантируют, что число запишется как "245.5" (с точкой), а не "245,5" (с запятой)
                    string durationStr = seconds.ToString("G", System.Globalization.CultureInfo.InvariantCulture);

                    List<string?> Data = new List<string?>
                        {
                            video.Url,
                            durationStr
                        };

                    return Data;
                }
            }

            return null;
        }



        private static readonly Dictionary<string, (string url, DateTime expiry)> _urlCache = new();

        public async Task<string> GetCachedDirectUrlAsync(string videoUrl)
        {
            // Если ссылка есть в кэше и она не старше 2 часов
            if (_urlCache.TryGetValue(videoUrl, out var cached) && cached.expiry > DateTime.Now)
            {
                Console.WriteLine("[CACHE] Используем сохраненную ссылку");
                return cached.url;
            }

            // Если в кэше нет — ищем через yt-dlp
            var ytInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp.exe",
                Arguments = $"-g -f bestaudio \"{videoUrl}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var ytProcess = Process.Start(ytInfo);
            string directUrl = (await ytProcess.StandardOutput.ReadToEndAsync()).Trim();

            // Сохраняем на 2 часа (YouTube ссылки обычно живут дольше, но так безопаснее)
            _urlCache[videoUrl] = (directUrl, DateTime.Now.AddHours(2));

            return directUrl;
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
            return Process.Start(ffmpegInfo);
        }

        //public Process GetFFmpegAudioProcess(string videoUrl, int seekSeconds = 0)
        //{
        //    // 1. Получаем URL (быстро)
        //    var ytInfo = new ProcessStartInfo
        //    {
        //        FileName = "yt-dlp.exe",
        //        Arguments = $"-g -f bestaudio \"{videoUrl}\"",
        //        RedirectStandardOutput = true,
        //        UseShellExecute = false,
        //        CreateNoWindow = true
        //    };
        //    using var ytProcess = Process.Start(ytInfo);
        //    string directUrl = ytProcess.StandardOutput.ReadToEnd().Trim();
        //    // Убираем WaitForExit, ReadToEnd и так дождется конца потока

        //    // 2. FFmpeg
        //    string seekTime = TimeSpan.FromSeconds(seekSeconds).ToString(@"hh\:mm\:ss");
        //    var ffmpegInfo = new ProcessStartInfo
        //    {
        //        FileName = "ffmpeg.exe",
        //        // Добавили -avoid_negative_ts make_zero для стабильности перемотки
        //        Arguments = $"-ss {seekTime} -i \"{directUrl}\" -avoid_negative_ts make_zero -acodec pcm_s16le -f s16le -ar 44100 -ac 2 -loglevel quiet -",
        //        RedirectStandardOutput = true,
        //        UseShellExecute = false,
        //        CreateNoWindow = true
        //    };
        //    return Process.Start(ffmpegInfo);
        //}




        //public Process GetFFmpegAudioProcess(string videoUrl, int seekSeconds = 0)
        //{
        //    // 1. Получаем прямую ссылку через yt-dlp (она работает стабильнее манифестов)
        //    var ytInfo = new ProcessStartInfo
        //    {
        //        FileName = "yt-dlp.exe",
        //        Arguments = $"-g -f bestaudio \"{videoUrl}\"",
        //        RedirectStandardOutput = true,
        //        UseShellExecute = false,
        //        CreateNoWindow = true
        //    };
        //    var ytProcess = Process.Start(ytInfo);
        //    string directUrl = ytProcess.StandardOutput.ReadToEnd().Trim();
        //    ytProcess.WaitForExit();

        //    // 2. Запускаем FFmpeg, который будет конвертировать это в сырой PCM
        //    string seekTime = TimeSpan.FromSeconds(seekSeconds).ToString(@"hh\:mm\:ss");

        //    var ffmpegInfo = new ProcessStartInfo
        //    {
        //        FileName = "ffmpeg.exe",
        //        // ВАЖНО: -ss {seekTime} ПЕРЕД -i делает перемотку мгновенной
        //        Arguments = $"-ss {seekTime} -i \"{directUrl}\" -acodec pcm_s16le -f s16le -ar 44100 -ac 2 -loglevel quiet -",
        //        RedirectStandardOutput = true,
        //        UseShellExecute = false,
        //        CreateNoWindow = true
        //    };
        //    return Process.Start(ffmpegInfo);
        //}










        /// /// стримы старые ниже(алгоритмы не трогать)



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
            //var manifest = await _yt.Videos.Streams.GetManifestAsync(videoId);
            //var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

            var manifest = await _yt.Videos.Streams.GetManifestAsync(videoId);

            // Фильтруем, чтобы получить только Mp4 (m4a), который WPF точно проглотит
            var streamInfo = manifest.GetAudioOnlyStreams()
                .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.Mp4)
                .GetWithHighestBitrate();

            // На случай, если mp4 вдруг не нашелся (крайне редко)
            if (streamInfo == null)
                streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();

            return streamInfo.Url;
        }



        public async Task<TrackDto2?> GetSimilarTrackAsync(string artist, string track, List<string> exclude)
        {
            string apiKey = "2852e900527a499032a3066ae34bb7ca";
            string workerUrl = "https://delicate-tooth-0e89.wellernam1788.workers.dev/";
            string workerUrl2 = "https://render-worker-zjwk.onrender.com";

            using var client = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true });



            try
            {
                // Лимит 100, чтобы было из чего выбирать после фильтрации
                string lastFmUrl = $"https://ws.audioscrobbler.com/2.0/?method=track.getsimilar&artist={Uri.EscapeDataString(artist)}&track={Uri.EscapeDataString(track)}&api_key={apiKey}&format=json&limit=100";
                System.Diagnostics.Debug.WriteLine(lastFmUrl);
                string finalUrl = $"{workerUrl}?url={Uri.EscapeDataString(lastFmUrl)}";
                System.Diagnostics.Debug.WriteLine(finalUrl);


                string rawLastFmUrl = $"https://ws.audioscrobbler.com/2.0/?method=track.getsimilar&artist={artist}&track={track}&api_key={apiKey}&format=json&limit=100";
                System.Diagnostics.Debug.WriteLine("rawLastFmUrl  " + rawLastFmUrl);

                // Кодируем всю колбасу целиком один раз для параметра ?url=
                string finalUrl2 = $"{workerUrl2}?url={Uri.EscapeDataString(rawLastFmUrl)}";
                System.Diagnostics.Debug.WriteLine("final2  " + finalUrl2);

                var response = await client.GetFromJsonAsync<JsonElement>(finalUrl2);

                if (response.TryGetProperty("similartracks", out var similarTracks))
                {
                    var trackList = similarTracks.GetProperty("track").EnumerateArray()
                     // Фильтруем исходные JsonElement, не изменяя их тип:
                     .Where(t =>
                     {
                         // Безопасно извлекаем значения (с проверкой на существование свойств)
                         string? artist = t.TryGetProperty("artist", out var artEl) && artEl.TryGetProperty("name", out var artNameEl)
                             ? artNameEl.GetString()
                             : null;

                         string? title = t.TryGetProperty("name", out var nameEl)
                             ? nameEl.GetString()
                             : null;

                         // Если имя артиста или трека не найдены — пропускаем элемент
                         if (artist == null || title == null)
                             return false;

                         // Проверяем по вашему списку исключений (регистронезависимо)
                         string key = $"{artist.ToLower()} - {title.ToLower()}";
                         return !exclude.Contains(key);
                     })
                     // Собираем отфильтрованные JsonElement в итоговый список
                     .ToList();



                    if (trackList.Count > 0)
                    {

                        var random = new Random();
                        var selected = trackList[random.Next(0, Math.Min(5, trackList.Count))];

                        string nextArtist = selected.GetProperty("artist").GetProperty("name").GetString();
                        string nextTrack = selected.GetProperty("name").GetString();


                        string dzSearchUrl = $"https://api.deezer.com/search?q=artist:\"{Uri.EscapeDataString(nextArtist)}\" track:\"{Uri.EscapeDataString(nextTrack)}\"&limit=1";
                        var dzResponse = await client.GetFromJsonAsync<JsonElement>(dzSearchUrl);

                        string imageUrl = "";

                        if (dzResponse.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                        {
                            var firstMatch = data.EnumerateArray().First();
                            imageUrl = firstMatch.GetProperty("album").GetProperty("cover_big").GetString();
                        }


                        return new TrackDto2(
                            nextTrack,
                            nextArtist,
                            "",
                            nextArtist,
                            nextTrack,
                            imageUrl
                        );
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка рекомендаций: {ex.Message}");
            }

            return null;
        }


        public async Task<TrackDto2?> GetTopTracksByArtistAsync(string artist, List<string> exclude)
        {
            string apiKey = "2852e900527a499032a3066ae34bb7ca";
            string workerUrl2 = "https://render-worker-zjwk.onrender.com";

            using var client = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true });

            try
            {
                // Метод artist.gettoptracks вернет самые популярные треки этого исполнителя
                string rawLastFmUrl = $"https://audioscrobbler.com{Uri.EscapeDataString(artist)}&api_key={apiKey}&format=json&limit=50";
                string finalUrl2 = $"{workerUrl2}?url={Uri.EscapeDataString(rawLastFmUrl)}";

                var response = await client.GetFromJsonAsync<JsonElement>(finalUrl2);

                if (response.TryGetProperty("toptracks", out var topTracks))
                {
                    var trackList = topTracks.GetProperty("track").EnumerateArray()
                        .Where(t =>
                        {
                            string? artName = t.TryGetProperty("artist", out var artEl) && artEl.TryGetProperty("name", out var artNameEl)
                                ? artNameEl.GetString() : null;
                            string? title = t.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;

                            if (artName == null || title == null) return false;

                            string key = $"{artName.ToLower()} - {title.ToLower()}";
                            return !exclude.Contains(key);
                        })
                        .ToList();

                    if (trackList.Count > 0)
                    {
                        // Выбираем случайный трек из топ-10 этого артиста
                        var random = new Random();
                        var selected = trackList[random.Next(0, Math.Min(10, trackList.Count))];

                        string nextArtist = selected.GetProperty("artist").GetProperty("name").GetString();
                        string nextTrack = selected.GetProperty("name").GetString();

                        // Ищем обложку в Deezer
                        string dzSearchUrl = $"https://api.deezer.com/search?q=artist:\"{Uri.EscapeDataString(nextArtist)}\" track:\"{Uri.EscapeDataString(nextTrack)}\"&limit=1";
                        string imageUrl = "";
                        try
                        {
                            var dzResponse = await client.GetFromJsonAsync<JsonElement>(dzSearchUrl);
                            if (dzResponse.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                            {
                                imageUrl = data.GetArrayLength() > 0 ? data.EnumerateArray().First().GetProperty("album").GetProperty("cover_big").GetString() : "";
                            }
                        }
                        catch { }

                        return new TrackDto2(nextTrack, nextArtist, "", nextArtist, nextTrack, imageUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка фолбэка по артисту: {ex.Message}");
            }

            return null;
        }







        public async Task<List<TrackDto2>> GetSimilarTracksBatchAsync(string artist, string track, List<string> exclude, int batchSize = 15)
        {
            string apiKey = "2852e900527a499032a3066ae34bb7ca";
            string workerUrl2 = "https://render-worker-zjwk.onrender.com";

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
                        // Берем не 1, а целую пачку (до batchSize)
                        var selectedBatch = trackList.Take(batchSize).ToList();

                        // Ищем обложки в Deezer параллельно!
                        var deezerTasks = selectedBatch.Select(async selected =>
                        {
                            string nextArtist = selected.GetProperty("artist").GetProperty("name").GetString();
                            string nextTrack = selected.GetProperty("name").GetString();
                            string imageUrl = "";

                            try
                            {
                                string dzSearchUrl = $"https://api.deezer.com/search?q=artist:\"{Uri.EscapeDataString(nextArtist)}\" track:\"{Uri.EscapeDataString(nextTrack)}\"&limit=1";
                                var dzResponse = await client.GetFromJsonAsync<JsonElement>(dzSearchUrl);
                                if (dzResponse.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                                {
                                    var firstMatch = data.EnumerateArray().First();
                                    imageUrl = firstMatch.GetProperty("album").GetProperty("cover_big").GetString();
                                }
                            }
                            catch { /* Игнорим ошибку дизера, просто будет без картинки */ }

                            return new TrackDto2(nextTrack, nextArtist, "", nextArtist, nextTrack, imageUrl);
                        });

                        // Ждем выполнения всех запросов к Deezer разом
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
            // Метод Last.fm: artist.gettoptracks
            string rawUrl = $"https://ws.audioscrobbler.com/2.0/?method=artist.gettoptracks&artist={Uri.EscapeDataString(artist)}&api_key=2852e900527a499032a3066ae34bb7ca&format=json&limit=50";
            return await FetchAndParseLastFm(rawUrl, "toptracks", "track", exclude, batchSize);
        }

        // Метод фолбэка: Глобальные популярные треки (Charts)
        public async Task<List<TrackDto2>> GetGlobalTrendingBatchAsync(List<string> exclude, int batchSize = 15)
        {
            // Метод Last.fm: chart.gettoptracks
            string rawUrl = $"https://ws.audioscrobbler.com/2.0/?method=chart.gettoptracks&api_key=2852e900527a499032a3066ae34bb7ca&format=json&limit=50";
            return await FetchAndParseLastFm(rawUrl, "tracks", "track", exclude, batchSize);
        }

        // Универсальный парсер, чтобы не дублировать код Deezer и прочее
        private async Task<List<TrackDto2>> FetchAndParseLastFm(string rawUrl, string rootProp, string listProp, List<string> exclude, int batchSize)
        {
            string workerUrl2 = "https://render-worker-zjwk.onrender.com";
            using var client = new HttpClient();
            string finalUrl = $"{workerUrl2}?url={Uri.EscapeDataString(rawUrl)}";

            try
            {
                var response = await client.GetFromJsonAsync<JsonElement>(finalUrl);
                if (response.TryGetProperty(rootProp, out var root))
                {
                    var tracks = root.GetProperty(listProp).EnumerateArray()
                        .Where(t => {
                            string art = t.TryGetProperty("artist", out var a) && a.TryGetProperty("name", out var n) ? n.GetString() : "";
                            string name = t.TryGetProperty("name", out var nm) ? nm.GetString() : "";
                            return !string.IsNullOrEmpty(art) && !exclude.Contains($"{art.ToLower()} - {name.ToLower()}");
                        })
                        .Take(batchSize);

                    // Тут просто переиспользуем твою логику с Deezer (можно вынести в отдельный метод)
                    // Для краткости тут просто возврат, но лучше вызвать ту же логику с Deezer как у тебя
                    return tracks.Select(t => new TrackDto2(t.GetProperty("name").GetString(), t.GetProperty("artist").GetProperty("name").GetString(), "", "", "", "")).ToList();
                }
            }
            catch { }
            return new List<TrackDto2>();
        }







    }


}
