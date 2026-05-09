using Microsoft.AspNetCore.Mvc;

namespace backendxd.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogoutController : Controller
    {
        [HttpPost]
        public IActionResult Logout()
        {
            // Очищаем cookie с токеном аутентификации
            Response.Cookies.Delete("auth_token", new CookieOptions
            {
                HttpOnly = true,
                Path = "/",
                SameSite = SameSiteMode.Lax,
                Secure = false // Установите true для HTTPS
            });

            // Устанавливаем CORS-заголовки
            HttpContext.Response.Headers.Append("Access-Control-Allow-Origin", "http://localhost");
            HttpContext.Response.Headers.Append("Access-Control-Allow-Credentials", "true");

            return Ok(new { status = "logged_out" });
        }
    }
}
