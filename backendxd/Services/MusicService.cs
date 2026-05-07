//using YoutubeExplode;
//using YoutubeExplode.Common;
//using YoutubeExplode.Search;
//using YoutubeExplode.Videos.Streams;
//using Microsoft.Extensions.Caching.Memory;
//using SoundCloudExplode;
//using SoundCloudExplode.Common;
//using SoundCloudExplode.Tracks;


using backendxd.DTOS;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
//using YoutubeExplode;
//using YoutubeExplode.Common;
//using YoutubeExplode.Playlists;
//using YoutubeExplode.Search;
//using YoutubeExplode.Videos;
//using YoutubeExplode.Videos.Streams;
using YouTubeMusicAPI.Client;
using YouTubeMusicAPI.Models.Search;

namespace backendxd.Services
{
    public class MusicService
    {

        private readonly HttpClient _http;
        //private readonly YoutubeClient _yt;
        private readonly IMemoryCache _cache;
        private readonly YouTubeMusicClient _ytm;
        private readonly Dictionary<string, int> _authorOccurrence = new();
        public MusicService(IMemoryCache cache)
        {
            _http = new HttpClient();
            // Ставим User-Agent, чтобы Яндекс и Ютуб не банили сразу
            _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            //_yt = new YoutubeClient();
            _cache = cache;
            _ytm = new YouTubeMusicClient();
        }

        //public async Task<List<TrackDto>> SearchAsync(string query)
        //{
        //    try
        //    {
        //        // Поиск треков
        //        var searchResults = await _soundcloud.Search.GetTracksAsync(query);

        //        return searchResults.Take(10).Select(t => new TrackDto(
        //            t.Id.ToString(),
        //            t.Title,
        //            t.User?.Username ?? "Unknown Artist",
        //            t.ArtworkUrl?.ToString() ?? "",
        //            t.Url
        //        )).ToList();
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new Exception($"SoundCloud search failed: {ex.Message}");
        //    }
        //}

        //public async Task<string> GetAudioStreamUrl(string trackIdentifier)
        //{
        //    try
        //    {
        //        string trackUrl = trackIdentifier;

        //        // Если в строке нет "soundcloud.com", значит это ID. 
        //        // Превращаем его в URL, который библиотека поймет.
        //        if (!trackIdentifier.Contains("soundcloud.com"))
        //        {
        //            trackUrl = $"https://soundcloud.com{trackIdentifier}";
        //        }

        //        var streamUrl = await _soundcloud.Tracks.GetDownloadUrlAsync(trackUrl);
        //        return streamUrl;
        //    }
        //    catch (Exception ex)
        //    {
        //        // Теперь ты увидишь реальную причину ошибки, если она останется
        //        throw new Exception($"SoundCloud streaming failed: {ex.Message}");
        //    }
        //}

        public async Task<List<TrackDto>> SearchAsync(string query)
        {
            var trackList = new List<TrackDto>();
            try
            {
                var searchPages = _ytm.SearchAsync(query);

                await foreach (dynamic result in searchPages) 
                {
                    
                    string artistName = "Unknown Artist";
                    try
                    {
                        if (result.Artists != null && result.Artists.Count > 0)
                        {
                            artistName = result.Artists[0].Name;
                        }
                    }
                    catch { }

                    // 2. Достаем жанр (Category)
                    string genreName = "Music";
                    try
                    {
                        genreName = result.Category?.ToString() ?? "Music";
                        // Если категория — "Albums", "Songs" или "Videos", это не жанр.
                        // В таком случае оставляем "Music" или пробуем взять имя артиста для поиска
                        if (genreName == "Albums" || genreName == "Songs" || genreName == "Videos")
                            genreName = "Music";
                    }
                    catch { }

                    Console.WriteLine("Id " + result.Id);
                    Console.WriteLine("Name " + result.Name);

                    Console.WriteLine("artistName " + artistName);

                    Console.WriteLine("genreName " + genreName);


                    trackList.Add(new TrackDto(
                        result.Id, // Используем Id из IntelliSense
                        result.Name, // Используем Name для заголовка
                        artistName,
                        genreName,
                        // Исправляем ошибку CS1977: берем последний элемент вместо OrderBy
                        result.Thumbnails?.LastOrDefault()?.Url ?? "",
                        $"https://www.youtube.com/watch?v={result.Id}"
                    ));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SEARCH ERROR] {ex.Message}");
            }
            return trackList;
        }


        public async Task<string> GetAudioStreamUrl(string videoId)
        {
            try
            {
                var streamingData = await _ytm.GetStreamingDataAsync(videoId);

                if (streamingData?.StreamInfo == null) return string.Empty;

                // Берем первый элемент из коллекции StreamInfo, у которого есть Url
                // Раз IntelliSense видит Length и ElementAt, значит это массив/список
                var stream = streamingData.StreamInfo.FirstOrDefault(s => !string.IsNullOrEmpty(s.Url));

                return stream?.Url ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Ошибка стрима: {ex.Message}");
                return string.Empty;
            }


            //var manifest = await _yt.Videos.Streams.GetManifestAsync(videoId);
            //var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            //return streamInfo.Url;
        }


        public async Task<List<TrackDto>> GetNextTrackAsync(string sourceUrl, int count = 10)
        {
            // 1. Достаем чистый ID (11 символов)
            string videoId = ParseVideoId(sourceUrl);

            // 2. Получаем инфо о треке через СТАБИЛЬНЫЙ поиск (не GetSongVideoInfo)
            var sourceTrack = await GetTrackInfoViaSearch(videoId);
            string genre = sourceTrack?.Genre ?? "Music";
            string sourceAuthor = sourceTrack?.Author ?? "Unknown";

            List<TrackDto> result = new List<TrackDto>();
            HashSet<string> seenIds = new HashSet<string> { videoId };
            Dictionary<string, int> authorCount = new Dictionary<string, int>();

            // 3. Ищем треки того же жанра/стиля
            // Запрос вида "Author Genre Mix" или просто "Genre songs"
            var searchPages = _ytm.SearchAsync($"{sourceAuthor} {genre} mix");

            try
            {
                await foreach (var page in searchPages)
                {
                    if (page is YouTubeMusicAPI.Models.Search.SongSearchResult song)
                    {
                        // УСЛОВИЯ:
                        // 1. Не тот же трек
                        if (seenIds.Contains(song.Id)) continue;

                        string currentArtist = song.Artists?.FirstOrDefault()?.Name ?? "Various";

                        // 2. Ограничение: первоначальный автор не более 3 раз
                        if (currentArtist == sourceAuthor)
                        {
                            if (authorCount.ContainsKey(currentArtist) && authorCount[currentArtist] >= 3) continue;
                        }

                        // Считаем авторов
                        if (!authorCount.ContainsKey(currentArtist)) authorCount[currentArtist] = 0;
                        authorCount[currentArtist]++;

                        result.Add(new TrackDto(
                            song.Id,
                            song.Name,
                            currentArtist,
                            genre,
                            song.Thumbnails?.LastOrDefault()?.Url ?? "",
                            $"https://www.youtube.com/watch?v={song.Id}"
                        ));

                        seenIds.Add(song.Id);
                    }

                    if (result.Count >= count) break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Search failed: {ex.Message}");
            }

            return result;
        }




        private string ParseVideoId(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            string id = url.Contains("v=")
                ? url.Split("v=").Last().Split('&').First()
                : url.Split('/').Last().Split('?').First();
            return id.Length > 11 ? id.Substring(0, 11) : id;
        }

        private async Task<TrackDto?> GetTrackInfoViaSearch(string videoId)
        {
            try
            {
                // Ищем конкретный ID. Это в 10 раз стабильнее, чем метод Info
                var search = _ytm.SearchAsync(videoId);
                await foreach (var item in search)
                {
                    if (item is YouTubeMusicAPI.Models.Search.SongSearchResult s)
                    {
                        return new TrackDto(
                            s.Id,
                            s.Name,
                            s.Artists?.FirstOrDefault()?.Name ?? "Unknown",
                            "Genre", // Позже можно вытянуть из названия полки или метаданных
                            "",
                            ""
                        );
                    }
                }
            }
            catch { }
            return null;
        }















        //старый некст трек
        //public async Task<TrackDto?> GetNextTrackAsync(string currentTrackUrl, string userId = "default")
        //{
        //    try
        //    {
        //        // 1. Извлекаем ID (исправлено с кавычками)
        //        //string videoId = currentTrackUrl.Split("v=").Last().Split('&').First();
        //        string videoId = "";
        //        if (currentTrackUrl.Contains("v="))
        //        {
        //            videoId = currentTrackUrl.Split("v=").Last().Split('&').First();
        //        }
        //        else
        //        {
        //            videoId = currentTrackUrl.Split('/').Last().Split('?').First();
        //        }
        //        if (videoId.Length > 11) videoId = videoId.Substring(0, 11);

        //        // 2. Кэш и история
        //        string cacheKey = $"history_{userId}";
        //        string authorCacheKey = $"history_authors_{userId}";
        //        if (!_cache.TryGetValue(cacheKey, out List<string>? historyIds)) historyIds = new List<string>();
        //        if (!_cache.TryGetValue(authorCacheKey, out List<string>? historyAuthors)) historyAuthors = new List<string>();

        //        // 3. Инфо о текущем треке (Artists вместо Details)

        //        //var trackInfo = (dynamic)null;
        //        string currentAuthor = "Various Artists";
        //        try
        //        {
        //            // Вместо GetSongVideoInfo, который падает с токеном, 
        //            // просто ищем этот же ID через поиск. Поиск обычно возвращает SongSearchResult.
        //            var searchById = _ytm.SearchAsync(videoId);
        //            await foreach (var item in searchById)
        //            {
        //                if (item is YouTubeMusicAPI.Models.Search.SongSearchResult s && s.Id == videoId)
        //                {
        //                    currentAuthor = s.Artists?.FirstOrDefault()?.Name ?? "Various Artists";
        //                    break;
        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"[WARN] Could not get track info for {videoId}: {ex.Message}");
        //            Console.WriteLine($"[WARN] Info parser failed for {videoId}, using default search.");

        //            // Если инфу не получили, currentAuthor останется "Unknown", и поиск будет по "Unknown mix"
        //        }

        //        // Обновляем историю
        //        if (!historyIds.Contains(videoId))
        //        {
        //            historyIds.Add(videoId);
        //            historyAuthors.Add(currentAuthor);
        //            if (historyIds.Count > 30) { historyIds.RemoveAt(0); historyAuthors.RemoveAt(0); }
        //            var cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1));
        //            _cache.Set(cacheKey, historyIds, cacheOptions);
        //            _cache.Set(authorCacheKey, historyAuthors, cacheOptions);
        //        }

        //        // 4. ПОЛУЧЕНИЕ РЕКОМЕНДАЦИЙ (Безопасный поиск)
        //        List<TrackDto> candidates = new List<TrackDto>();

        //        try
        //        {
        //            // МЕНЯЕМ "Radio" на "mix" или "music", чтобы избежать ошибки парсера
        //            //var searchPages = _ytm.SearchAsync($"{currentAuthor} mix");
        //            string searchQuery = currentAuthor != "Unknown" ? $"{currentAuthor} mix" : "Top hits music";
        //            var searchPages = _ytm.SearchAsync(searchQuery);

        //            if (searchPages != null)
        //            {
        //                await foreach (var page in searchPages)
        //                {
        //                    // На основе твоих скринов, SongSearchResult — это и есть песня
        //                    if (page is YouTubeMusicAPI.Models.Search.SongSearchResult song)
        //                    {
        //                        candidates.Add(new TrackDto(
        //                            song.Id,
        //                            song.Name,
        //                            song.Artists?.FirstOrDefault()?.Name ?? currentAuthor,
        //                            // Берем последний Thumbnail (самый большой)
        //                            song.Thumbnails?.LastOrDefault()?.Url ?? "",
        //                            $"https://www.youtube.com/watch?v={song.Id}"
        //                        ));
        //                    }
        //                    // Если попалась полка с песнями (Shelf)
        //                    else if (page.GetType().GetProperty("Music") != null)
        //                    {
        //                        var musicList = (IEnumerable<dynamic>)page.GetType().GetProperty("Music").GetValue(page);
        //                        foreach (var m in musicList) candidates.Add(MapToTrackDto(m, currentAuthor));
        //                    }

        //                    if (candidates.Count > 15) break;
        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            // Теперь даже если парсинг упадет, программа пойдет дальше к фолбэку
        //            Console.WriteLine($"[WARN] Search failed: {ex.Message}");
        //        }

        //        // 5. ЛОГИКА ВЫБОРА
        //        if (candidates.Count == 0) return await GetYouTubeRelatedFallback(videoId, historyIds, new List<string>(), historyAuthors);

        //        string lastAuthor = historyAuthors.LastOrDefault() ?? "";
        //        int comboCount = historyAuthors.Where(a => a == lastAuthor).Count();

        //        var nextTrack = candidates
        //            .Where(c => c.Id != videoId && !historyIds.Contains(c.Id))
        //            .OrderByDescending(c => (c.Artist != lastAuthor ? 150 : 100) - (comboCount * 50))
        //            .ThenBy(_ => Guid.NewGuid())
        //            .FirstOrDefault();

        //        return nextTrack;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"[CRITICAL] Music Logic Failed: {ex.Message}");
        //        return null;
        //    }
        //}
        //private TrackDto MapToTrackDto(dynamic t, string defaultAuthor)
        //{
        //    // Берем самое большое превью через индекс, чтобы избежать CS1977
        //    string thumbUrl = "";
        //    if (t.Thumbnails != null && t.Thumbnails.Count > 0)
        //    {
        //        thumbUrl = t.Thumbnails[t.Thumbnails.Count - 1].Url;
        //    }

        //    return new TrackDto(
        //        t.Id,
        //        t.Name,
        //        t.Artists?.FirstOrDefault()?.Name ?? defaultAuthor,
        //        thumbUrl,
        //        $"https://www.youtube.com/watch?v={t.Id}"
        //    );
        //}


        /// <summary>
        /// ///очень старый некст трек
        /// </summary>
        /// <param name="videoId"></param>
        /// <param name="historyIds"></param>
        /// <param name="historyTitles"></param>
        /// <param name="historyAuthors"></param>
        /// <returns></returns>

        //public async Task<TrackDto?> GetNextTrackAsync(string currentTrackUrl, string userId = "default")
        //{
        //    try
        //    {

        //        string cacheKey = $"history_{userId}";
        //        string titleCacheKey = $"history_titles_{userId}";
        //        string authorCacheKey = $"history_authors_{userId}";

        //        if (!_cache.TryGetValue(cacheKey, out List<string>? historyIds)) historyIds = new List<string>();
        //        if (!_cache.TryGetValue(titleCacheKey, out List<string>? historyTitles)) historyTitles = new List<string>();
        //        if (!_cache.TryGetValue(authorCacheKey, out List<string>? historyAuthors)) historyAuthors = new List<string>();

        //        // Получаем инфу о текущем треке
        //        var metadata = await _yt.Videos.GetAsync(currentTrackUrl);

        //        string normalizeTitle(string t) => Regex.Replace(t.ToLower(), @"[^\w]", "");
        //        string currentNorm = normalizeTitle(metadata.Title);

        //        // --- Логика обновления кэша ---
        //        if (!historyIds.Contains(metadata.Id.Value))
        //        {
        //            historyIds.Add(metadata.Id.Value);
        //            historyTitles.Add(currentNorm);
        //            historyAuthors.Add(metadata.Author.ChannelTitle); // Теперь автор тоже в теме

        //            // Держим историю в узде (не больше 30 элементов)
        //            if (historyIds.Count > 30)
        //            {
        //                historyIds.RemoveAt(0);
        //                historyTitles.RemoveAt(0);
        //                // Авторов можно чистить так же, чтобы индекс совпадал
        //                if (historyAuthors.Count > 30) historyAuthors.RemoveAt(0);
        //            }

        //            // Сохраняем всё обратно в кэш
        //            var cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1));

        //            _cache.Set(cacheKey, historyIds, cacheOptions);
        //            _cache.Set(titleCacheKey, historyTitles, cacheOptions);
        //            _cache.Set(authorCacheKey, historyAuthors, cacheOptions);
        //        }



        //        // 1. ОЧИСТКА ЗАПРОСА (Критично для Яндекса)
        //        // Убираем всё, кроме букв и цифр, чтобы не ловить 400 Bad Request
        //        string cleanTitle = Regex.Replace(metadata.Title, @"[^\w\sа-яА-Я]", " ").Trim();
        //        // Берем первые 2-3 слова названия, чтобы не искать "нарезку", а искать "тему"
        //        var words = cleanTitle.Split(' ').Where(w => w.Length > 2).Take(3);
        //        //string query = string.Join(" ", words);
        //        string query = $"{metadata.Author.ChannelTitle} best songs";

        //        Console.WriteLine($"[LOG] Пробуем веб-поиск Яндекса: {query}");

        //        // 2. ЗАПРОС К ВЕБ-ХЕНДЛЕРУ (без токена)
        //        string searchUrl = $"https://music.yandex.ru/api/v2/search/suggest?text={Uri.EscapeDataString(query)}";

        //        // 2. Обязательно добавляем заголовки (Яндекс стал капризным)
        //        var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
        //        request.Headers.Add("Accept", "application/json");
        //        request.Headers.Add("Referer", "https://music.yandex.ru/"); // Добавь это!
        //        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36...");


        //        var response = await _http.SendAsync(request);

        //        if (!response.IsSuccessStatusCode)
        //        {
        //            Console.WriteLine($"[LOG] Яндекс ответил кодом: {response.StatusCode}. Пробую альтернативу...");
        //            // Если 404 или 400 — выходим и идем в YouTube Related
        //            return await GetYouTubeRelatedFallback(metadata.Id.Value, historyIds, historyTitles, historyAuthors);
        //        }

        //        var searchData = await response.Content.ReadFromJsonAsync<JsonElement>();

        //        // В веб-версии структура другая: searchData -> tracks -> items
        //        if (!searchData.TryGetProperty("tracks", out var tracks) ||
        //            !tracks.TryGetProperty("items", out var items) ||
        //            items.GetArrayLength() == 0)
        //        {
        //            Console.WriteLine("Ошибка searchData");
        //            return null;
        //        }

        //        // Берем ID трека
        //        var trackId = items[0].GetProperty("id").ToString();

        //        // 3. ПОЛУЧАЕМ ПОХОЖИЕ (тоже через веб-хендлер)
        //        string similarUrl = $"https://music.yandex.ru/handlers/track-similar.jsx?track={trackId}";
        //        var similarData = await _http.GetFromJsonAsync<JsonElement>(similarUrl);

        //        if (!similarData.TryGetProperty("similars", out var similars) || similars.GetArrayLength() == 0)
        //        {
        //            return null;
        //        }


        //        var nextItem = similars[new Random().Next(0, Math.Min(3, similars.GetArrayLength()))];
        //        var nextTitle = nextItem.GetProperty("title").GetString();
        //        var nextArtist = nextItem.GetProperty("artists")[0].GetProperty("name").GetString();
        //        var cover = nextItem.GetProperty("coverUri").GetString()?.Replace("%%", "400x400") ?? "";

        //        Console.WriteLine($"[LOG] Рекомендация найдена: {nextArtist} - {nextTitle}");




        //        var ytSearch = _yt.Search.GetVideosAsync($"{nextArtist} - {nextTitle}");
        //        YoutubeExplode.Search.VideoSearchResult? target = null;
        //        await foreach (var res in ytSearch) { target = res; break; }

        //        if (target == null) return null;

        //        return new TrackDto(
        //            target.Id.Value,
        //            nextTitle ?? "Unknown",
        //            nextArtist ?? "Unknown",
        //            "https://" + cover,
        //            target.Url
        //        );
        //    }

        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Ошибка алгоритма: {ex.Message}");
        //        Console.WriteLine($"[CRITICAL ERROR]: {ex.Message}");
        //        return null;
        //    }
        //}


        //старый фолбэк 



        //private async Task<TrackDto?> GetYouTubeRelatedFallback(string videoId, List<string> historyIds, List<string> historyTitles, List<string> historyAuthors)
        //{
        //    try
        //    {
        //        var trackInfo = await _ytm.GetSongVideoInfoAsync(videoId);
        //        string currentAuthor = trackInfo.Artists?.FirstOrDefault()?.Name ?? "Unknown";

        //        // КРИТИЧНО: Убираем "Radio" здесь, заменяем на "mix", чтобы не было крэша

        //        string searchQuery = currentAuthor != "Unknown" ? $"{currentAuthor} mix" : "Top hits music";
        //        var searchPages = _ytm.SearchAsync(searchQuery);

        //        //var searchPages = _ytm.SearchAsync($"{currentAuthor} mix");

        //        List<TrackDto> candidates = new List<TrackDto>();

        //        if (searchPages != null)
        //        {
        //            await foreach (var page in searchPages)
        //            {
        //                // Проверяем, не является ли сама страница результатом-песней
        //                if (page is YouTubeMusicAPI.Models.Search.SongSearchResult song)
        //                {
        //                    candidates.Add(new TrackDto(
        //                        song.Id,
        //                        song.Name,
        //                        song.Artists?.FirstOrDefault()?.Name ?? currentAuthor,
        //                        song.Thumbnails?.LastOrDefault()?.Url ?? "",
        //                        $"https://www.youtube.com/watch?v={song.Id}"
        //                    ));
        //                }
        //                // Иначе проверяем полку (Shelf)
        //                else if (page.Category.ToString().Contains("Songs") || page.Name == "Songs")
        //                {
        //                    // Используем Music или Items в зависимости от того, что доступно
        //                    var items = (page as dynamic).Music as IEnumerable<dynamic> ?? (page as dynamic).Items as IEnumerable<dynamic>;

        //                    if (items != null)
        //                    {
        //                        foreach (var t in items)
        //                        {
        //                            candidates.Add(MapToTrackDto(t, currentAuthor));
        //                        }
        //                    }
        //                }
        //                if (candidates.Count > 20) break;
        //            }
        //        }

        //        // Остальная логика Scoring (CalculateLogicScore) остается без изменений
        //        var nextTrack = candidates
        //            .Where(t => t.Id != videoId && !historyIds.Contains(t.Id))
        //            .Select(t => new { Track = t, Score = CalculateLogicScore(t, currentAuthor, 0, historyTitles) })
        //            .OrderByDescending(x => x.Score)
        //            .FirstOrDefault()?.Track;

        //        return nextTrack ?? candidates.FirstOrDefault(t => t.Id != videoId);
        //    }
        //    catch (Exception ex)
        //    {
        //        // Теперь здесь не будет "Required token is null", если убрать слово Radio
        //        Console.WriteLine($"[ERROR] Fallback failed: {ex.Message}");
        //        return null;
        //    }
        //}

        //private int CalculateLogicScore(TrackDto t, string lastAuthor, int comboCount, List<string> historyTitles)
        //{
        //    int score = 100;
        //    string title = t.Title.ToLower();

        //    // 1. ЖЕСТКОЕ РАЗНООБРАЗИЕ
        //    if (t.Artist != lastAuthor)
        //    {
        //        score += 150; // Плюс за смену автора
        //    }
        //    else
        //    {
        //        // Штраф за "комбо" одного автора
        //        score -= (comboCount * 80);
        //    }

        //    // 2. ФИЛЬТР ПОВТОРОВ (по названию)
        //    string normTitle = Regex.Replace(title, @"[^\w]", "");
        //    if (historyTitles.Any(ht => normTitle.Contains(ht))) score -= 300;

        //    // 3. ФИЛЬТР МУСОРА
        //    if (title.Contains("full album") || title.Contains("live at")) score -= 100;

        //    return score;
        //}



















        //private async Task<TrackDto?> GetYouTubeRelatedFallback(string videoId, List<string> historyIds, List<string> historyTitles, List<string> historyAuthors)
        //{
        //    try
        //    {
        //        var metadata = await _yt.Videos.GetAsync(videoId);


        //        //string searchQuery = $"{metadata.Author.ChannelTitle} full album mix"; // Ищем шире
        //        string searchQuery = $"{metadata.Author.ChannelTitle} Official Radio";

        //        var results = await _yt.Search.GetVideosAsync(searchQuery).CollectAsync(30);

        //        int recentAuthorCount = historyAuthors.TakeLast(5).Count(a => a == metadata.Author.ChannelTitle);

        //        var nextVideo = results.FirstOrDefault(v =>
        //    v.Id.Value != videoId &&
        //    !historyIds.Contains(v.Id.Value) &&
        //    // Проверка на повтор названия
        //    !historyTitles.Any(oldTitle =>
        //        Regex.Replace(v.Title.ToLower(), @"[^\w]", "").Contains(oldTitle)) &&
        //    // Главный фильтр разнообразия:
        //    // Если артист уже надоел, берем ТОЛЬКО другого автора
        //    (recentAuthorCount < 2 || v.Author.ChannelTitle != metadata.Author.ChannelTitle) &&
        //    // Убираем видео длиннее 15 минут (чтобы не попадать на "Full Albums")
        //    (v.Duration == null || v.Duration < TimeSpan.FromMinutes(15))
        //);

        //        //if (filteredResults.Any())
        //        //{
        //        //    // Берем рандомный, но не самый первый (самый первый часто дубль)
        //        //    int index = filteredResults.Count > 1 ? new Random().Next(1, filteredResults.Count) : 0;
        //        //    nextVideo = filteredResults[index];
        //        //}
        //        //else
        //        //{
        //        //    // ПЛАН Б: Если фильтры всё съели, берем просто любое видео, кроме текущего
        //        //    nextVideo = results.FirstOrDefault(v => v.Id.Value != videoId);
        //        //}

        //        //if (nextVideo == null) nextVideo = results.Skip(new Random().Next(1, 5)).FirstOrDefault();
        //        if (nextVideo == null)
        //        {
        //            // План Б: Берем первое попавшееся видео, которое НЕ принадлежит текущему автору
        //            nextVideo = results.FirstOrDefault(v =>
        //                v.Id.Value != videoId &&
        //                v.Author.ChannelTitle != metadata.Author.ChannelTitle);

        //            // Если и таких нет, тогда просто любой рандом
        //            if (nextVideo == null)
        //                nextVideo = results.Where(v => v.Id.Value != videoId).OrderBy(x => Guid.NewGuid()).FirstOrDefault();
        //        }

        //        if (nextVideo == null)
        //        {
        //            Console.WriteLine("[LOG] YouTube не выдал результатов даже по широкому поиску.");
        //            return null;
        //        }

        //        return new TrackDto(
        //            nextVideo.Id.Value,
        //            nextVideo.Title,
        //            nextVideo.Author.ChannelTitle,
        //            nextVideo.Thumbnails.OrderByDescending(t => t.Resolution.Width).FirstOrDefault()?.Url ?? "",
        //            nextVideo.Url
        //        );
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"[LOG] Ошибка поиска: {ex.Message}");
        //        return null;
        //    }
        //}


    }



}

