using backendxd.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backendxd.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class MeController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MeController(AppDbContext context)
        {
            _context = context;
        }


        [Authorize]

        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {

            var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
               ?? User.FindFirst("name")?.Value
               ?? User.Identity?.Name;

            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }


            var user = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email
                })
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                return NotFound(new { error = "USER_NOT_FOUND" });
            }

            return Ok(user);
        }
    }
}
