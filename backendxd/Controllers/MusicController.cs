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
        private readonly ILogger<MusicController> _logger;

        public MusicController(MusicService2 musicService, AppDbContext context, ILogger<MusicController> logger)
        {
            _musicService = musicService;
            _context = context;
            _logger = logger;
        }


        [HttpGet("get-url")]
        public async Task<string> GetUrl(string artist, string track)
        {
            _logger.LogInformation("Запрос на получение прямой ссылки: {Artist} - {Track}", artist, track);
            var ytTrack = await _musicService.SearchOnYouTubeAsync(artist, track);

            return await _musicService.GetCachedDirectUrlAsync(ytTrack!.Url);
        }




        [HttpGet("stream")]


        public async Task<List<string>> GetStream(string artist, string track)
        {
            _logger.LogInformation("Запрос потока для трека: {Artist} - {Track}", artist, track);
            var ytTrackdata = await _musicService.SearchOnYouTubeAsync3(artist, track);
            if (ytTrackdata == null)
            {
                _logger.LogWarning("Поток для трека {Artist} - {Track} не найден (404)", artist, track);
                Response.StatusCode = 404;
                return [""];
            }

            //return ytTrackdata;
            return ytTrackdata.Where(x => x != null).Cast<string>().ToList();

        }





        [HttpPost("toggle")]
        public async Task<IActionResult> Toggle([FromBody] FavoriteTrackDto req)
        {
            _logger.LogInformation("Пользователь {User} переключает статус 'Избранное' для трека: {Artist} - {Title}", req.UserName, req.Author, req.Title);
            try
            {
                var existing = await _context.FavoriteTracks
                .FirstOrDefaultAsync(f => f.Username == req.UserName
                                       && f.Title == req.Title
                                       && f.Author == req.Author);

                if (existing != null)
                {

                    _context.FavoriteTracks.Remove(existing);
                    _logger.LogInformation("Трек {Artist} - {Title} удален из избранного у пользователя {User}", req.Author, req.Title, req.UserName);
                }
                else
                {

                    _context.FavoriteTracks.Add(new FavoriteTrack
                    {
                        Username = req.UserName,
                        Title = req.Title,
                        Author = req.Author,
                        ImageUrl = req.ImageUrl,
                    });
                    _logger.LogInformation("Трек {Artist} - {Title} добавлен в избранное пользователю {User}", req.Author, req.Title, req.UserName);
                }

                await _context.SaveChangesAsync();
                return Ok(new { isFavorite = existing == null });
            }
            catch (Exception ex)
            {
                // Запись критической ошибки СУБД со Stack Trace для отчета
                _logger.LogCritical(ex, "Критическая ошибка при работе с Neon DB в методе Toggle для пользователя {User}", req.UserName);
                return StatusCode(500, "Внутренняя ошибка при работе с базой данных");
            }
        }


        [Authorize]
        [HttpGet("getName")]

        public async Task<IActionResult> getName()
        {
            var username = GetUsername();
            _logger.LogInformation("Запрос имени пользователя из JWT. Обнаружен: {Username}", username);
            await Task.CompletedTask;
            return Ok(username);
        }
        private string GetUsername() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
        ?? User.Identity?.Name ?? "";

        [HttpGet("listFavorites")]
        public async Task<IActionResult> GetFavorites()
        {
            var username = GetUsername();
            _logger.LogInformation("Запрос списка избранного для пользователя: {Username}", username);
            //if (string.IsNullOrEmpty(username)) return Unauthorized();
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Попытка несанкционированного доступа к списку избранного без токена");
                return Unauthorized();
            }

            try
            {
                var list = await _context.FavoriteTracks
                    .Where(f => f.Username == username)
                    .ToListAsync();

                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Ошибка получения списка избранного из базы данных для {Username}", username);
                return StatusCode(500, "Ошибка СУБД");
            }
        }







        [HttpPost("GetNextRecommended")]
        public async Task<IActionResult> GetNextRecommended2([FromBody] GetNextRecommendedRequest request)
        {
            _logger.LogInformation("Запрос рекомендаций для: {Artist} - {Track}", request.Artist, request.Track);
            var excludedList = request.Exclude?.Select(x => x.ToLower()).ToList() ?? new List<string>();


            var recommendedBatch = await _musicService.GetSimilarTracksBatchAsync(request.Artist ?? "", request.Track ?? "", excludedList, 15);

            if (recommendedBatch.Count == 0)
            {
                Console.WriteLine($"[server] ⚠️ Фолбэк по артисту: {request.Artist}");
                _logger.LogWarning("Похожие треки не найдены. Фолбэк по артисту: {Artist}", request.Artist);
                recommendedBatch = await _musicService.GetTopTracksByArtistBatchAsync(request.Artist ?? "", excludedList, 15);
            }

            if (recommendedBatch.Count == 0)
            {
                _logger.LogWarning("Треки артиста не найдены. Глобальный фолбэк (Charts)");
                Console.WriteLine("[server] ⚠️ Глобальный фолбэк (Charts)");
                recommendedBatch = await _musicService.GetGlobalTrendingBatchAsync(excludedList, 15);
            }

            if (recommendedBatch.Count == 0)
            {
                _logger.LogError("Все уровни рекомендаций вернули пустой список для артиста {Artist}", request.Artist);
                return NotFound();
            }


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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Не удалось зарезолвить YouTube-информацию для рекомендованного трека {Artist} - {Title}", rec.Author, rec.Title);
                }
                return null;
            });


            var results = await Task.WhenAll(youtubeTasks);


            var validResults = results.Where(r => r != null).ToList();

            if (validResults.Count == 0) return NotFound();

            return Ok(validResults);
        }




        public class GetNextRecommendedRequest
        {
            public string? Artist { get; set; }
            public string? Track { get; set; }
            public List<string>? Exclude { get; set; }
        }




        [HttpGet("search")]
        public async Task<ActionResult<object>> Search([FromQuery] string query)
        {

            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogWarning("Передан пустой поисковый запрос");
                return BadRequest("Search query cannot be empty");
            }

            _logger.LogInformation("Выполняется умный поиск по запросу: '{Query}'", query);

            try
            {

                dynamic results = await _musicService.SmartSearchAsync2(query);


                if (results.Artists.Count == 0 && results.Tracks.Count == 0)
                {
                    _logger.LogWarning("По запросу '{Query}' ничего не найдено", query);
                    return NotFound("No artists or tracks found for this query");
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка выполнения поиска по запросу '{Query}'", query);
                Console.WriteLine($"Search Error: {ex.Message}");
                return StatusCode(500, "Internal server error during search");
            }
        }

        [HttpGet("album/{id}")]
        public async Task<ActionResult<List<TrackDto2>>> GetAlbumTracks(long id)
        {
            _logger.LogInformation("Запрос треков альбома ID: {AlbumId}", id);
            var tracks = await _musicService.GetAlbumTracksAsync(id);

            if (tracks == null || !tracks.Any())
                return NotFound("Альбом не найден или пуст");

            return Ok(tracks);
        }

        [HttpGet("artist/{id}/albums")]
        public async Task<ActionResult<List<AlbumDto>>> GetArtistAlbums(long id)
        {
            _logger.LogInformation("Запрос альбомов артиста ID: {ArtistId}", id);
            var albums = await _musicService.GetArtistAlbumsAsync(id);

            if (albums == null || !albums.Any())
                return NotFound("Альбомы не найдены");

            return Ok(albums);
        }






        [HttpPost("create-playlist")]
        public async Task<IActionResult> CreatePlaylist([FromBody] CreatePlaylistDto dto)
        {
            _logger.LogInformation("Запрос на создание плейлиста '{Playlist}' для пользователя {User}", dto.PlaylistName, dto.Username);

            if (string.IsNullOrWhiteSpace(dto.PlaylistName) || string.IsNullOrWhiteSpace(dto.Username))
            {
                return BadRequest(new { message = "Название плейлиста и имя пользователя обязательны" });
            }
            try
            {

                bool exists = await _context.PlaylistsTracks.AnyAsync(p =>
                p.Username == dto.Username && p.PlaylistName == dto.PlaylistName);

                if (exists)
                {
                    _logger.LogWarning("Плейлист '{Playlist}' у пользователя {User} уже существует", dto.PlaylistName, dto.Username);
                    return BadRequest(new { message = "Плейлист с таким названием уже существует!" });
                }

                var newPlaylistRow = new PlaylistTrack
                {
                    PlaylistName = dto.PlaylistName,
                    Username = dto.Username,
                    TrackTitle = null,
                    TrackArtist = null,
                    ImageUrl = null
                };

                _context.PlaylistsTracks.Add(newPlaylistRow);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Плейлист успешно создан!" });
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Критический сбой СУБД при создании плейлиста '{Playlist}' для {User}", dto.PlaylistName, dto.Username);
                return StatusCode(500, "Ошибка базы данных");
            }
        }


        [HttpPost("add-track-to-playlist")]
        public async Task<IActionResult> AddTrackToPlaylist([FromBody] AddTrackDto dto)
        {
            _logger.LogInformation("Добавление трека '{Artist} - {Title}' в плейлист '{Playlist}' пользователя {User}", dto.TrackArtist, dto.TrackTitle, dto.PlaylistName, dto.Username);


            try
            {
                var playlistExists = await _context.PlaylistsTracks.AnyAsync(p =>
                p.Username == dto.Username && p.PlaylistName == dto.PlaylistName);

                if (!playlistExists)
                {
                    return NotFound(new { message = "Плейлист не найден" });
                }


                var emptyRow = await _context.PlaylistsTracks.FirstOrDefaultAsync(p =>
                    p.Username == dto.Username &&
                    p.PlaylistName == dto.PlaylistName &&
                    p.TrackTitle == null);

                if (emptyRow != null)
                {

                    emptyRow.TrackTitle = dto.TrackTitle;
                    emptyRow.TrackArtist = dto.TrackArtist;
                    emptyRow.ImageUrl = dto.ImageUrl;
                }
                else
                {

                    var newTrackRow = new PlaylistTrack
                    {
                        PlaylistName = dto.PlaylistName,
                        Username = dto.Username,
                        TrackTitle = dto.TrackTitle,
                        TrackArtist = dto.TrackArtist,
                        ImageUrl = dto.ImageUrl
                    };
                    _context.PlaylistsTracks.Add(newTrackRow);
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Трек добавлен в плейлист!" });
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Ошибка СУБД при добавлении трека в плейлист '{Playlist}'", dto.PlaylistName);
                return StatusCode(500, "Ошибка базы данных");
            }
        }


        [HttpPost("user-all-playLists")]
        public async Task<IActionResult> GetUserPlaylists([FromBody] GetUserPlaylistsDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username))
                return BadRequest(new { message = "Имя пользователя обязательно" });

            var playlists = await _context.PlaylistsTracks
                .Where(p => p.Username == dto.Username)
                .GroupBy(p => p.PlaylistName)
                .Select(g => new
                {
                    PlaylistName = g.Key,
                    ImageUrl = g.FirstOrDefault(x => x.ImageUrl != null) != null
                               ? g.FirstOrDefault(x => x.ImageUrl != null)!.ImageUrl
                               : null
                })
                .ToListAsync();

            return Ok(playlists);
        }


        [HttpPost("playlist-tracks")]
        public async Task<IActionResult> GetPlaylistTracks([FromBody] GetPlaylistTracksDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.PlaylistName))
                return BadRequest(new { message = "Не все поля заполнены" });

            var tracks = await _context.PlaylistsTracks
                .Where(p => p.Username == dto.Username && p.PlaylistName == dto.PlaylistName && p.TrackTitle != null)
                .Select(p => new
                {
                    p.Id,
                    Title = p.TrackTitle,
                    Artist = p.TrackArtist,
                    ImageUrl = p.ImageUrl
                })
                .ToListAsync();

            return Ok(tracks);
        }

        [HttpPost("remove-track")]
        public async Task<IActionResult> RemoveTrackFromPlaylist([FromBody] RemoveTrackByFieldsDto dto)
        {

            _logger.LogInformation("Удаление трека '{Title}' из плейлиста '{Playlist}' пользователя {User}", dto.TrackTitle, dto.PlaylistName, dto.Username);

            if (string.IsNullOrWhiteSpace(dto.Username) ||
                string.IsNullOrWhiteSpace(dto.PlaylistName) ||
                string.IsNullOrWhiteSpace(dto.TrackTitle))
            {
                return BadRequest(new { message = "Не все поля заполнены" });
            }

            try
            {
                var trackRow = await _context.PlaylistsTracks.FirstOrDefaultAsync(p =>
                p.Username == dto.Username &&
                p.PlaylistName == dto.PlaylistName &&
                p.TrackTitle == dto.TrackTitle &&
                p.TrackArtist == dto.TrackArtist);

                if (trackRow == null)
                    return NotFound(new { message = "Такой трек в плейлисте не найден" });


                var totalRows = await _context.PlaylistsTracks.CountAsync(p =>
                    p.Username == dto.Username && p.PlaylistName == dto.PlaylistName);

                if (totalRows == 1)
                {

                    trackRow.TrackTitle = null;
                    trackRow.TrackArtist = null;
                    trackRow.ImageUrl = null;
                }
                else
                {

                    _context.PlaylistsTracks.Remove(trackRow);
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Трек успешно удален из плейлиста" });
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Ошибка СУБД при удалении трека из плейлиста '{Playlist}'", dto.PlaylistName);
                return StatusCode(500, "Ошибка базы данных");
            }
        }

        [HttpPost("delete-playlist")]
        public async Task<IActionResult> DeletePlaylist([FromBody] DeletePlaylistDto dto)
        {
            _logger.LogInformation("Запрос на ПОЛНОЕ УДАЛЕНИЕ плейлиста '{Playlist}' пользователя {User}", dto.PlaylistName, dto.Username);

            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.PlaylistName))
                return BadRequest(new { message = "Не все поля заполнены" });


            try
            {
                var playlistRows = await _context.PlaylistsTracks
                .Where(p => p.Username == dto.Username && p.PlaylistName == dto.PlaylistName)
                .ToListAsync();

                if (playlistRows.Count == 0)
                {
                    return NotFound(new { message = "Плейлист не найден" });
                }

                _context.PlaylistsTracks.RemoveRange(playlistRows);
                await _context.SaveChangesAsync();

                return Ok(new { message = $"Плейлист \"{dto.PlaylistName}\" полностью удален" });
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Ошибка СУБД при удалении плейлиста '{Playlist}' для пользователя {User}", dto.PlaylistName, dto.Username);
                return StatusCode(500, "Ошибка базы данных");
            }
        }


    }





    public class CreatePlaylistDto
    {
        public string PlaylistName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }

    public class AddTrackDto
    {
        public string PlaylistName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string TrackTitle { get; set; } = string.Empty;
        public string TrackArtist { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class GetUserPlaylistsDto
    {
        public string Username { get; set; } = string.Empty;
    }

    public class GetPlaylistTracksDto
    {
        public string Username { get; set; } = string.Empty;
        public string PlaylistName { get; set; } = string.Empty;
    }

    public class RemoveTrackByFieldsDto
    {
        public string Username { get; set; } = string.Empty;
        public string PlaylistName { get; set; } = string.Empty;
        public string TrackTitle { get; set; } = string.Empty;
        public string TrackArtist { get; set; } = string.Empty;
    }

    public class DeletePlaylistDto
    {
        public string Username { get; set; } = string.Empty;
        public string PlaylistName { get; set; } = string.Empty;
    }


}

