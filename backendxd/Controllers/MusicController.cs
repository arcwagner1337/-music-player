using backendxd.DTOS;
using backendxd.Services;
using Microsoft.AspNetCore.Mvc;


namespace backendxd.Controllers
{
    [ApiController]
    [Route("api/music")]
    public class MusicController : ControllerBase
    {
        private readonly MusicService2 _musicService;

        public MusicController(MusicService2 musicService)
        {
            _musicService = musicService;
        }


        [HttpGet("stream")]
        
        public async Task<IActionResult> GetStream(string artist, string track)
        {
            var streamUrl = await _musicService.GetFullStreamByTrackInfoAsync(artist, track);

            if (string.IsNullOrEmpty(streamUrl))
                return NotFound("Не удалось найти аудио-поток");

            
            return Ok(new { url = streamUrl });
        }

        [HttpGet("search")]
        public async Task<ActionResult<SearchResultDto>> Search([FromQuery] string query)
        {
            
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Search query cannot be empty");
            }

            try
            {
                
                var results = await _musicService.SmartSearchAsync2(query);

                
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
            
            var recommendedTrack = await _musicService.GetSimilarTrackAsync(artist, track);

            
            if (recommendedTrack == null)
            {
                return NotFound("Не удалось найти похожий трек");
            }

            
            return Ok(recommendedTrack);
        }

    }
}
