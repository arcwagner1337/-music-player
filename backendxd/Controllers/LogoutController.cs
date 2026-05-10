using Microsoft.AspNetCore.Mvc;

namespace backendxd.Controllers
{
    [ApiController]
    [Route("api/logout")]
    public class LogoutController : ControllerBase // Лучше наследовать от ControllerBase для API
    {
        [HttpPost]
        public IActionResult Logout()
        {
            // Удаляем куку (важно, чтобы параметры Path и Domain совпадали с теми, что были при создании)
            Response.Cookies.Delete("auth_token", new CookieOptions
            {
                HttpOnly = true,
                Path = "/",
                Secure = true, // Ставь true, так как у тебя https://localhost
                SameSite = SameSiteMode.Lax
            });

            return Ok(new { status = "logged_out" });
        }
    }
}
