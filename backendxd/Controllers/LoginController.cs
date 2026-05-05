using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;

using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using backendxd.DTOS;
using LoginRequest = backendxd.DTOS.LoginRequest;

namespace backendxd.Controllers
{
    [ApiController]
    [Route("api/login")] 
    public class LoginController : Controller
    {
        private readonly IConfiguration _config;
        // потом тут будет DBContext

        public LoginController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
           
            // потом будет: var user = _context.Users.FirstOrDefault(u => u.Username == request.Username);
            if (request.Username == "Arcwagner" && request.Password == "1337")
            {
                var claims = new[]
                {
                new Claim(ClaimTypes.Name, request.Username),
                new Claim("issuer", "void_core")
            };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JWT_SECRET"] ?? "default_secret_key_32_chars!!"));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: "void_core",
                    claims: claims,
                    expires: DateTime.Now.AddDays(7),
                    signingCredentials: creds
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

               
                Response.Cookies.Append("auth_token", tokenString, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true, 
                    SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromDays(7)
                });

                return Ok(new { status = "success", username = request.Username });
            }

            return Unauthorized(new { error = "Unauthorized" });
        }

    }
}
