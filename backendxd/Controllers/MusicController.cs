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

        //[HttpGet("search")]
        //public async Task<IActionResult> Search([FromQuery] string q)
        //{
        //    if (string.IsNullOrWhiteSpace(q)) return BadRequest("Query is empty");

        //    var results = await _musicService.SearchAsync(q);
        //    return Ok(results);
        //}

        //[HttpGet("stream")]
        //public async Task<IActionResult> GetStream([FromQuery] string url)
        //{
        //    if (string.IsNullOrEmpty(url)) return BadRequest("Url is empty");

        //    try
        //    {
        //        var streamUrl = await _musicService.GetAudioStreamUrl(url);
        //        return Ok(new { streamUrl });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { error = ex.Message });
        //    }
        //}





        [HttpGet("GetSimilarTrack")]
        public async Task<ActionResult<TrackDto2>> GetRecommendation([FromQuery] string artist, [FromQuery] string track)
        {
            // 1. Вызываем наш "умный" метод
            var recommendedTrack = await _musicService.GetSimilarTrackAsync(artist, track);

            // 2. Если Last.fm ничего не нашел или YouTube подвел
            if (recommendedTrack == null)
            {
                return NotFound("Не удалось найти похожий трек");
            }

            // 3. Возвращаем 200 OK с нашей дтошкой
            return Ok(recommendedTrack);
        }

    }
}
