using backendxd.Services;
using Microsoft.AspNetCore.Mvc;

namespace backendxd.Controllers
{
    [ApiController]
    [Route("api/music")]
    public class MusicController : ControllerBase
    {
        private readonly MusicService _musicService;

        public MusicController(MusicService musicService)
        {
            _musicService = musicService;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest("Query is empty");

            var results = await _musicService.SearchAsync(q);
            return Ok(results);
        }

        [HttpGet("stream")]
        public async Task<IActionResult> GetStream([FromQuery] string url)
        {
            if (string.IsNullOrEmpty(url)) return BadRequest("Url is empty");

            try
            {
                var streamUrl = await _musicService.GetAudioStreamUrl(url);
                return Ok(new { streamUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
