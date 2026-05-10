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
        // Этот атрибут заставит проверять JWT
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            // 1. Извлекаем имя пользователя из Claims (оно там, если ты передавал его в GenerateJwtToken)
            var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
               ?? User.FindFirst("name")?.Value
               ?? User.Identity?.Name;

            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }

            // 2. Ищем пользователя в БД
            var user = await _context.Users
                .Select(u => new { // Не отправляем пароль на фронт ради безопасности
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
