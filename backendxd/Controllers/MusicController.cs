using backendxd.DTOS;
using backendxd.Services;
using Microsoft.AspNetCore.Mvc;
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

        public MusicController(MusicService2 musicService)
        {
            _musicService = musicService;
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


        [HttpGet("GetNextRecommended")]
        public async Task<IActionResult> GetNextRecommended(string artist, string track, [FromQuery] string[] exclude)
        {
            // Передаем exclude в сервис
            var excludedList = exclude?.Select(x => x.ToLower()).ToList() ?? new List<string>();

            var recommended = await _musicService.GetSimilarTrackAsync(artist, track, excludedList);
            if (recommended == null) return NotFound();

            var ytInfo = await _musicService.SearchOnYouTubeAsync3(recommended.Author, recommended.Title);
            if (ytInfo == null) return NotFound();

            return Ok(new
            {
                Artist = recommended.Author,
                Title = recommended.Title,
                ImageUrl = recommended.ImageUrl,
                StreamUrl = ytInfo[0],
                Duration = ytInfo[1]
            });
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
