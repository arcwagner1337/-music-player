using backendxd.Data;
using backendxd.DTOS;
using backendxd.Models;
using backendxd.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;


namespace backendxd.Controllers
{
    [ApiController]
    [Route("api/music")]
    public class MusicController : ControllerBase
    {
        private readonly MusicService2 _musicService;
        private readonly YoutubeClient _yt = new YoutubeClient();
        private readonly AppDbContext _context;

        public MusicController(MusicService2 musicService, AppDbContext context)
        {
            _musicService = musicService;
            _context = context;
        }


        [HttpGet("get-url")]
        public async Task<string> GetUrl(string artist, string track)
        {
            var ytTrack = await _musicService.SearchOnYouTubeAsync(artist, track);
            return await _musicService.GetCachedDirectUrlAsync(ytTrack.Url);
        }




        [HttpGet("stream")]

        
        public async Task<List<string>> GetStream(string artist, string track)
        {
            var ytTrackdata = await _musicService.SearchOnYouTubeAsync3(artist, track);
            if (ytTrackdata == null) { Response.StatusCode = 404; return [""]; }

            return ytTrackdata;

            // Сначала получаем ссылку (из кэша или через yt-dlp)
            //string directUrl = await _musicService.GetCachedDirectUrlAsync(ytTrack.Url);

            //// Запускаем FFmpeg сразу
            //using var ffmpegProcess = _musicService.GetFFmpegAudioProcess(directUrl, seek);

            //Response.ContentType = "audio/l16";
            //await ffmpegProcess.StandardOutput.BaseStream.CopyToAsync(Response.Body);
        }


        //[HttpGet("GetNextRecommended")]
        //public async Task<IActionResult> GetNextRecommended(string artist, string track, [FromQuery] string[] exclude)
        //{
        //    // Передаем exclude в сервис
        //    var excludedList = exclude?.Select(x => x.ToLower()).ToList() ?? new List<string>();

        //    var recommended = await _musicService.GetSimilarTrackAsync(artist, track, excludedList);
        //    if (recommended == null) return NotFound();

        //    var ytInfo = await _musicService.SearchOnYouTubeAsync3(recommended.Author, recommended.Title);
        //    if (ytInfo == null) return NotFound();

        //    return Ok(new
        //    {
        //        Artist = recommended.Author,
        //        Title = recommended.Title,
        //        ImageUrl = recommended.ImageUrl,
        //        StreamUrl = ytInfo[0],
        //        Duration = ytInfo[1]
        //    });
        //}


     

        [HttpPost("toggle")]
        public async Task<IActionResult> Toggle([FromBody] FavoriteTrackDto req)
        {
            // Ищем трек в базе
            var existing = await _context.FavoriteTracks
                .FirstOrDefaultAsync(f => f.Username == req.UserName
                                       && f.Title == req.Title
                                       && f.Author == req.Author);

            if (existing != null)
            {
                // Если есть — удаляем (отлайкиваем)
                _context.FavoriteTracks.Remove(existing);
            }
            else
            {
                // Если нет — добавляем
                _context.FavoriteTracks.Add(new FavoriteTrack
                {
                    Username = req.UserName,
                    Title = req.Title,
                    Author = req.Author,
                    ImageUrl = req.ImageUrl, // Убедись, что добавил Url в модель FavoriteTrack
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { isFavorite = existing == null });
        }


        [Authorize]
        [HttpGet("getName")]

        public async Task<IActionResult> getName()
        {
            var username = GetUsername();

            return Ok(username);
        }
        private string GetUsername() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
        ?? User.Identity?.Name ?? "";

        [HttpGet("listFavorites")] 
        public async Task<IActionResult> GetFavorites()
        {
            var username = GetUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var list = await _context.FavoriteTracks
                .Where(f => f.Username == username)
                .ToListAsync();

            return Ok(list);
        }




        [HttpPost("GetNextRecommended0")]
        public async Task<IActionResult> GetNextRecommended([FromBody] GetNextRecommendedRequest request)
        {

            Console.WriteLine($"[server] artist: {request?.Artist}, track: {request?.Track}, exclude count: {request?.Exclude?.Count}");

            var excludedList = request.Exclude?.Select(x => x.ToLower()).ToList() ?? new List<string>();

            var recommended = await _musicService.GetSimilarTrackAsync(request.Artist, request.Track, excludedList);

            if (recommended == null)
            {
                Console.WriteLine($"[server] ⚠️ Трек [{request.Track}] не найден в Last.fm. Запуск фолбэка по артисту [{request.Artist}]...");
                recommended = await _musicService.GetTopTracksByArtistAsync(request.Artist, excludedList);
            }


            // fallback — ищем по другому артисту из истории
            if (recommended == null && excludedList.Count > 0)
            {
                var randomPast = excludedList[new Random().Next(excludedList.Count)];
                var parts = randomPast.Split(" - ", 2);
                if (parts.Length == 2)
                    recommended = await _musicService.GetSimilarTrackAsync(parts[0], parts[1], excludedList);
            }

            if (recommended == null) return NotFound();

            var ytInfo = await _musicService.SearchOnYouTubeAsync3(recommended.Author, recommended.Title);
            if (ytInfo == null) return NotFound();

            return Ok(new
            {
                artist = recommended.Author,
                title = recommended.Title,
                imageUrl = recommended.ImageUrl,
                streamUrl = ytInfo[0],
                duration = ytInfo[1]
            });
        }



        [HttpPost("GetNextRecommended")]
        public async Task<IActionResult> GetNextRecommended2([FromBody] GetNextRecommendedRequest request)
        {
            var excludedList = request.Exclude?.Select(x => x.ToLower()).ToList() ?? new List<string>();

            // 1. Пытаемся взять похожие
            var recommendedBatch = await _musicService.GetSimilarTracksBatchAsync(request.Artist, request.Track, excludedList, 15);

            // 2. Фолбэк по артисту
            if (recommendedBatch.Count == 0)
            {
                Console.WriteLine($"[server] ⚠️ Фолбэк по артисту: {request.Artist}");
                recommendedBatch = await _musicService.GetTopTracksByArtistBatchAsync(request.Artist, excludedList, 15);
            }

            // 3. Глобальный фолбэк (самый крайний)
            if (recommendedBatch.Count == 0)
            {
                Console.WriteLine("[server] ⚠️ Глобальный фолбэк (Charts)");
                recommendedBatch = await _musicService.GetGlobalTrendingBatchAsync(excludedList, 15);
            }

            if (recommendedBatch.Count == 0) return NotFound();

            // 2. Параллельно запрашиваем YouTube ссылки для всей пачки
            var youtubeTasks = recommendedBatch.Select(async rec =>
            {
                try
                {
                    var ytInfo = await _musicService.SearchOnYouTubeAsync3(rec.Author, rec.Title);
                    if (ytInfo != null)
                    {
                        return new
                        {
                            artist = rec.Author,
                            title = rec.Title,
                            imageUrl = rec.ImageUrl,
                            streamUrl = ytInfo[0],
                            duration = ytInfo.Count > 1 ? ytInfo[1] : "0"
                        };
                    }
                }
                catch { /* Игнорируем треки, которые не нашлись на ютубе */ }
                return null;
            });

            // Ждем все ютуб-запросы
            var results = await Task.WhenAll(youtubeTasks);

            // Отсеиваем те, что вернули null (не нашлись)
            var validResults = results.Where(r => r != null).ToList();

            if (validResults.Count == 0) return NotFound();

            // Возвращаем массив треков!
            return Ok(validResults);
        }




        public class GetNextRecommendedRequest
        {
            public string Artist { get; set; }
            public string Track { get; set; }
            public List<string> Exclude { get; set; }
        }


        //public async Task GetStream(string artist, string track, int seek = 0)
        //{
        //    Console.WriteLine($"[START] Запрос: {artist} - {track}");
        //    try
        //    {
        //        // 1. Ищем видео (твой старый добрый метод поиска)
        //        var ytTrack = await _musicService.SearchOnYouTubeAsync(artist, track);
        //        if (ytTrack == null)
        //        {
        //            Console.WriteLine("[ERROR] Трек не найден");
        //            Response.StatusCode = 404;
        //            return;
        //        }

        //        // 2. Получаем процесс FFmpeg, который уже начал тянуть звук
        //        // Важно использовать using, чтобы процесс убился, когда клиент отключится
        //        using var ffmpegProcess = _musicService.GetFFmpegAudioProcess(ytTrack.Url, seek);

        //        Console.WriteLine($"[OK] FFmpeg запущен для: {ytTrack.Url}");

        //        // 3. Настраиваем заголовки ответа
        //        // Говорим, что это сырой аудиопоток (PCM)
        //        Response.ContentType = "audio/l16";
        //        Response.StatusCode = 200;

        //        // 4. Гвоздь программы: перекачиваем байты из FFmpeg прямо в тело HTTP-ответа
        //        // Этот цикл будет работать, пока FFmpeg не выдаст весь трек или пока WPF не закроет соединение
        //        await ffmpegProcess.StandardOutput.BaseStream.CopyToAsync(Response.Body);

        //        Console.WriteLine("[DONE] Передача потока завершена");
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"[CRITICAL ERROR]: {ex.Message}");
        //        if (!Response.HasStarted) Response.StatusCode = 500;
        //    }
        //}


        //public async Task<IActionResult> GetStream(string artist, string track)
        //{
        //    Console.WriteLine($"[START] Запрос: {artist} - {track}");
        //    try
        //    {
        //        var ytTrack = await _musicService.SearchOnYouTubeAsync(artist, track);
        //        if (ytTrack == null)
        //        {
        //            Console.WriteLine("[ERROR] Трек не найден в поиске");
        //            return NotFound();
        //        }
        //        Console.WriteLine($"[OK] Нашли видео: {ytTrack.Url}");

        //        var manifest = await _yt.Videos.Streams.GetManifestAsync(ytTrack.Url);
        //        Console.WriteLine("[OK] Манифест получен");

        //        //var streamInfo = manifest.GetAudioOnlyStreams()
        //        //    .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.WebM)
        //        //    .GetWithHighestBitrate();

        //        var streamInfo = manifest.GetAudioOnlyStreams()
        //            .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.Mp4) // Пробуем MP4 контейнер
        //            .GetWithHighestBitrate();

        //        if (streamInfo == null)
        //        {
        //            Console.WriteLine("[ERROR] Поток не найден");
        //            return NotFound();
        //        }

        //        // ВАЖНО: попробуй сначала просто вернуть URL, чтобы проверить, работает ли поиск вообще
        //        // return Ok(streamInfo.Url); 

        //        Console.WriteLine($"[OK] Поток выбран: {streamInfo.Size}");
        //        var stream = await _yt.Videos.Streams.GetAsync(streamInfo);

        //        Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{Uri.EscapeDataString(track)}.mp4\"");
        //        Response.Headers.Add("Accept-Ranges", "bytes");
        //        Response.ContentType = "audio/mp4";

        //        Console.WriteLine("[OK] Стрим открыт, начинаю передачу...");

        //        //return File(stream, "audio/mp4", enableRangeProcessing: true);
        //        //return File(stream, "audio/webm", enableRangeProcessing: true);

        //        await stream.CopyToAsync(Response.Body, 65536);
        //        return new EmptyResult();

        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"[CRITICAL ERROR]: {ex.Message}");
        //        return StatusCode(500, ex.Message);
        //    }
        //}

        //public async Task<IActionResult> GetStream(string artist, string track)
        //{
        //    var streamUrl = await _musicService.GetFullStreamByTrackInfoAsync(artist, track);

        //    if (string.IsNullOrEmpty(streamUrl))
        //        return NotFound("Не удалось найти аудио-поток");


        //    return Ok(new { url = streamUrl });
        //}

        [HttpGet("search")]
        public async Task<ActionResult<object>> Search([FromQuery] string query)
        {

            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Search query cannot be empty");
            }

            try
            {

                dynamic results = await _musicService.SmartSearchAsync2(query);


                if (results.Artists.Count == 0 && results.Tracks.Count == 0)
                {
                    return NotFound("No artists or tracks found for this query");
                }

                return Ok(results);
            }
            catch (Exception ex)
            {

                Console.WriteLine($"Search Error: {ex.Message}");
                return StatusCode(500, "Internal server error during search");
            }
        }

        [HttpGet("album/{id}")]
        public async Task<ActionResult<List<TrackDto2>>> GetAlbumTracks(long id)
        {
            var tracks = await _musicService.GetAlbumTracksAsync(id);

            if (tracks == null || !tracks.Any())
                return NotFound("Альбом не найден или пуст");

            return Ok(tracks);
        }

        [HttpGet("artist/{id}/albums")]
        public async Task<ActionResult<List<AlbumDto>>> GetArtistAlbums(long id)
        {
            var albums = await _musicService.GetArtistAlbumsAsync(id);

            if (albums == null || !albums.Any())
                return NotFound("Альбомы не найдены");

            return Ok(albums);
        }





        [HttpGet("GetSimilarTrack")]
        public async Task<ActionResult<TrackDto2>> GetRecommendation([FromQuery] string artist, [FromQuery] string track)
        {

            var recommendedTrack = await _musicService.GetSimilarTrackAsync(artist, track, []);


            if (recommendedTrack == null)
            {
                return NotFound("Не удалось найти похожий трек");
            }


            return Ok(recommendedTrack);
        }

    }
}
