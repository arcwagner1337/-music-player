using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backendxd.Data;
using backendxd.Services;
using backendxd.Models;
using backendxd.DTOS;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace backendxd.Controllers
{
    [ApiController]
    [Route("api/register")]
    public class RegisterController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly GenerateJWT _jwtService;
        private readonly mail _mailService;

        public RegisterController(AppDbContext context, IConfiguration config, GenerateJWT jwtService, mail mailService)
        {
            _context = context;
            _config = config;
            _jwtService = jwtService;
            _mailService = mailService;
        }

       
        [HttpPost("request")] 
        public async Task<IActionResult> RequestRegister([FromBody] RegisterRequest request)
        {
           
            if (await _context.Users.AnyAsync(u => u.Username == request.Username || u.Email == request.Email))
                return BadRequest(new { error = "USER_ALREADY_EXISTS" });

            string code = new Random().Next(100000, 999999).ToString();

          
            var old = _context.PendingRegistrations.Where(p => p.email == request.Email);
            _context.PendingRegistrations.RemoveRange(old);

            var pending = new PendingRegistration
            {
                username = request.Username,
                email = request.Email,
                password = request.Password, // в идеале бы тут BCrypt.HashPassword(request.Password)
                code = code,
                expires_at = DateTime.UtcNow.AddMinutes(15)
            };

            _context.PendingRegistrations.Add(pending);
            await _context.SaveChangesAsync();

           

            await _mailService.SendEmailAsync(request.Email, code);

            return Ok(new { status = "code_sent" });
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> Confirm([FromBody] VerifyRequest request)
        {
            
            var pending = await _context.PendingRegistrations
                .FirstOrDefaultAsync(p => p.email == request.email && p.code == request.code);

            if (pending == null)
            {
                return BadRequest(new { error = "Invalid or expired code" });
            }

            
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                
                var newUser = new User
                {
                    Username = pending.username,
                    Email = pending.email,
                    Password = pending.password, //  BCrypt позже надо будет
                    SubStart = 0,
                    SubEnd = 0
                };
                _context.Users.Add(newUser);

                
                _context.PendingRegistrations.Remove(pending);

                await _context.SaveChangesAsync();

                
                var token = _jwtService.GenerateJwtToken(newUser.Username);

                
                Response.Cookies.Append("auth_token", token, new CookieOptions
                {
                    HttpOnly = true,
                    MaxAge = TimeSpan.FromDays(7),
                    SameSite = SameSiteMode.Lax,
                    Secure = true 
                });

                await transaction.CommitAsync();

                return Ok(new { status = "registration_complete", username = newUser.Username, token = token });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { error = "DB_ERROR", message = ex.Message });
            }
        }

    }
}
