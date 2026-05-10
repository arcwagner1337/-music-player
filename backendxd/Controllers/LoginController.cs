using backendxd.Data;
using backendxd.DTOS;
using backendxd.Services;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LoginRequest = backendxd.DTOS.LoginRequest;

namespace backendxd.Controllers
{
    [ApiController]
    [Route("api/login")] 
    public class LoginController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _context;
        private readonly GenerateJWT _jwtService;
        // потом тут будет DBContext

        public LoginController(IConfiguration config, AppDbContext context, GenerateJWT jwtService)
        {
            _config = config;
            _context = context;
            _jwtService = jwtService;
        }

        [HttpPost] // Будет доступен по POST /api/login
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // 1. Ищем юзера (по нику или почте)
            // Используем маленькие буквы полей, как в твоих моделях (username, password)
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Username);

            if (user == null)
            {
                return Unauthorized(new { error = "USER_NOT_FOUND" });
            }

            // 2. Проверяем пароль
            if (user.Password != request.Password)
            {
                return Unauthorized(new { error = "WRONG_PASSWORD" });
            }

            // 3. Генерируем токен через твой сервис
            var token = _jwtService.GenerateJwtToken(user.Username);

            // 4. Ставим куку
            Response.Cookies.Append("auth_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true, 
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromDays(7)
            });

            return Ok(new { status = "success", username = user.Username, token = token });
        }

    }
}
