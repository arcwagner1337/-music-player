using Microsoft.AspNetCore.Mvc;

namespace backendxd.Controllers
{
    [ApiController]
    [Route("api/logout")]
    public class LogoutController : ControllerBase
    {
        [HttpPost]
        public IActionResult Logout()
        {

            Response.Cookies.Delete("auth_token", new CookieOptions
            {
                HttpOnly = true,
                Path = "/",
                Secure = true,
                SameSite = SameSiteMode.Lax
            });

            return Ok(new { status = "logged_out" });
        }
    }
}
