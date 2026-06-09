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

            return await _musicService.GetCachedDirectUrlAsync(ytTrack!.Url);
        }




        [HttpGet("stream")]


        public async Task<List<string>> GetStream(string artist, string track)
        {
            var ytTrackdata = await _musicService.SearchOnYouTubeAsync3(artist, track);
            if (ytTrackdata == null) { Response.StatusCode = 404; return [""]; }

            //return ytTrackdata;
            return ytTrackdata.Where(x => x != null).Cast<string>().ToList();

        }





        [HttpPost("toggle")]
        public async Task<IActionResult> Toggle([FromBody] FavoriteTrackDto req)
        {

            var existing = await _context.FavoriteTracks
                .FirstOrDefaultAsync(f => f.Username == req.UserName
                                       && f.Title == req.Title
                                       && f.Author == req.Author);

            if (existing != null)
            {

                _context.FavoriteTracks.Remove(existing);
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
            }

            await _context.SaveChangesAsync();
            return Ok(new { isFavorite = existing == null });
        }


        [Authorize]
        [HttpGet("getName")]

        public async Task<IActionResult> getName()
        {
            var username = GetUsername();
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
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var list = await _context.FavoriteTracks
                .Where(f => f.Username == username)
                .ToListAsync();

            return Ok(list);
        }







        [HttpPost("GetNextRecommended")]
        public async Task<IActionResult> GetNextRecommended2([FromBody] GetNextRecommendedRequest request)
        {
            var excludedList = request.Exclude?.Select(x => x.ToLower()).ToList() ?? new List<string>();


            var recommendedBatch = await _musicService.GetSimilarTracksBatchAsync(request.Artist ?? "", request.Track ?? "", excludedList, 15);

            if (recommendedBatch.Count == 0)
            {
                Console.WriteLine($"[server] ⚠️ Фолбэк по артисту: {request.Artist}");
                recommendedBatch = await _musicService.GetTopTracksByArtistBatchAsync(request.Artist ?? "", excludedList, 15);
            }

            if (recommendedBatch.Count == 0)
            {
                Console.WriteLine("[server] ⚠️ Глобальный фолбэк (Charts)");
                recommendedBatch = await _musicService.GetGlobalTrendingBatchAsync(excludedList, 15);
            }

            if (recommendedBatch.Count == 0) return NotFound();


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
                catch { }
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






        [HttpPost("create-playlist")]
        public async Task<IActionResult> CreatePlaylist([FromBody] CreatePlaylistDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.PlaylistName) || string.IsNullOrWhiteSpace(dto.Username))
            {
                return BadRequest(new { message = "Название плейлиста и имя пользователя обязательны" });
            }


            bool exists = await _context.PlaylistsTracks.AnyAsync(p =>
                p.Username == dto.Username && p.PlaylistName == dto.PlaylistName);

            if (exists)
            {
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


        [HttpPost("add-track-to-playlist")]
        public async Task<IActionResult> AddTrackToPlaylist([FromBody] AddTrackDto dto)
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
            if (string.IsNullOrWhiteSpace(dto.Username) ||
                string.IsNullOrWhiteSpace(dto.PlaylistName) ||
                string.IsNullOrWhiteSpace(dto.TrackTitle))
            {
                return BadRequest(new { message = "Не все поля заполнены" });
            }


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

        [HttpPost("delete-playlist")]
        public async Task<IActionResult> DeletePlaylist([FromBody] DeletePlaylistDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.PlaylistName))
                return BadRequest(new { message = "Не все поля заполнены" });

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

