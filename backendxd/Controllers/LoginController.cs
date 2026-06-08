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
       

        public LoginController(IConfiguration config, AppDbContext context, GenerateJWT jwtService)
        {
            _config = config;
            _context = context;
            _jwtService = jwtService;
        }

        [HttpPost] 
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
           
            
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Username);

            if (user == null)
            {
                return Unauthorized(new { error = "USER_NOT_FOUND" });
            }

           
            if (user.Password != request.Password)
            {
                return Unauthorized(new { error = "WRONG_PASSWORD" });
            }

           
            var token = _jwtService.GenerateJwtToken(user.Username);

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
